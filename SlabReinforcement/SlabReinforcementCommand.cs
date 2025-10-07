using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Line = Autodesk.Revit.DB.Line;

namespace SlabReinforcement
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class SlabReinforcementCommand : IExternalCommand
    {
        Floor Floor;
        string DWFFilePath;
        double ScaleFactor;

        Transform Transform;
        string ConcreteClass;
        double AdjacentElementsTolerance;
        double ZoneMergeTolerance;
        string SelectedReinforcementDirection;

        public bool UseCutLengths;
        public string RoundIncrement;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                _ = GetPluginStartInfo();
            }
            catch { }

            // Основной код
            Document doc = commandData.Application.ActiveUIDocument.Document;
            Selection sel = commandData.Application.ActiveUIDocument.Selection;

            try
            {
                // Выбор плит для армирования
                FloorSelectionFilter selFilter = new FloorSelectionFilter();
                Reference selSlab = sel.PickObject(ObjectType.Element, selFilter, "Выберите плиту!");
                Floor = doc.GetElement(selSlab) as Floor;

                // Выбор DWG-подложки
#if R2019 || R2020 || R2021
                ScaleFactor = UnitUtils.ConvertToInternalUnits(1, DisplayUnitType.DUT_METERS);
#else
                ScaleFactor = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);
#endif
                ImportInstanceSelectionFilter importInstanceSelectionFilter = new ImportInstanceSelectionFilter();
                Reference selImportInstance = sel.PickObject(ObjectType.Element, importInstanceSelectionFilter, "Выберите DWG подложку!");
                ImportInstance importInstance = doc.GetElement(selImportInstance) as ImportInstance;
                Transform = importInstance.GetTransform();

                CADLinkType cadLinkType = doc.GetElement(importInstance.GetTypeId()) as CADLinkType;
                ModelPath modelPath = cadLinkType.GetExternalFileReference().GetAbsolutePath();
                DWFFilePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
            }
            catch
            {
                return Result.Cancelled;
            }


            var dxf = netDxf.DxfDocument.Load(DWFFilePath);
            var faces = dxf.Entities.Faces3D;

            ConcurrentBag<FiniteElementFace> finiteElementFaces = new ConcurrentBag<FiniteElementFace>();
            int faceId = 0; // Локальные счетчики для параллельных задач
            object lockObject = new object(); // Для управления faceId

            Parallel.ForEach(faces, face =>
            {
                if (face == null)
                    return;

                // Преобразуем вершины Face3D в список XYZ
                List<XYZ> vertices = new List<XYZ>
                {
                    new XYZ(face.FirstVertex.X * ScaleFactor, face.FirstVertex.Y * ScaleFactor, 0),
                    new XYZ(face.SecondVertex.X * ScaleFactor, face.SecondVertex.Y * ScaleFactor, 0),
                    new XYZ(face.ThirdVertex.X * ScaleFactor, face.ThirdVertex.Y * ScaleFactor, 0)
                };

                // Если четвертая вершина не совпадает с третьей (грань четырехугольная), добавляем ее
                if (!face.FourthVertex.Equals(face.ThirdVertex))
                {
                    vertices.Add(new XYZ(face.FourthVertex.X * ScaleFactor, face.FourthVertex.Y * ScaleFactor, 0));
                }

                // Применяем трансформацию из ImportInstance к каждой вершине
                for (int i = 0; i < vertices.Count; i++)
                {
                    vertices[i] = Transform.OfPoint(vertices[i]);
                    vertices[i] = new XYZ(vertices[i].X, vertices[i].Y, 0); // Обнуляем Z-координату
                }

                // Обработка цвета грани
                System.Drawing.Color faceColor = System.Drawing.Color.White;
                if (face.Color != null)
                {
                    faceColor = System.Drawing.Color.FromArgb(
                        face.Color.R,
                        face.Color.G,
                        face.Color.B
                    );
                }

                // Создаем конечный элемент с вершинами
                int localFaceId;
                lock (lockObject) // Гарантируем уникальность ID
                {
                    localFaceId = faceId++;
                }

                FiniteElementFace feFace = new FiniteElementFace(
                    localFaceId,
                    vertices,
                    new Color(faceColor.R, faceColor.G, faceColor.B)
                );

                // Проверка на уникальность
                bool isUnique = true;
                foreach (var existingFace in finiteElementFaces)
                {
                    if (AreVerticesEqual(existingFace.Vertices, feFace.Vertices))
                    {
                        isUnique = false;
                        break;
                    }
                }

                if (isUnique)
                {
                    finiteElementFaces.Add(feFace);
                }
            });


            //Словарь для подсчета количества элементов по цветам
            Dictionary<Color, int> colorCount = new Dictionary<Color, int>();

            foreach (var fe in finiteElementFaces)
            {
                if (colorCount.ContainsKey(fe.FaceColor))
                {
                    colorCount[fe.FaceColor]++;
                }
                else
                {
                    colorCount[fe.FaceColor] = 1;
                }
            }

            // Группируем элементы по цветам
            var groupedByColor = finiteElementFaces
                .GroupBy(fe => new { fe.FaceColor.Red, fe.FaceColor.Green, fe.FaceColor.Blue }) // Группируем по RGB
                .Select(group => new ColorStat
                {
                    Color = $"RGB({group.Key.Red}, {group.Key.Green}, {group.Key.Blue})",
                    ColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(
                        (byte)group.Key.Red,
                        (byte)group.Key.Green,
                        (byte)group.Key.Blue // Преобразуем цвет в SolidColorBrush
                    ))
                })
                .ToList();

            //Собираем типы армирования
            List<RebarBarType> availableRebarTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(rt => rt.Name, new AlphanumComparatorFastString())
                .ToList();

            // Показываем WPF окно с итоговыми данными'
            List<ColorStat> selectedColors = null;
            SlabReinforcementWPF statsWindow = new SlabReinforcementWPF(groupedByColor, availableRebarTypes);
            if (statsWindow.ShowDialog() == true)
            {
                selectedColors = statsWindow.GetSelectedColors();
            }
            else
            {
                return Result.Cancelled;
            }


            ConcreteClass = statsWindow.ConcreteClass;
            AdjacentElementsTolerance = double.TryParse(statsWindow.AdjacentElementsTolerance, out double adjacentTolerance) ? adjacentTolerance : 50.0;
            ZoneMergeTolerance = double.TryParse(statsWindow.ZoneMergeTolerance, out double zoneTolerance) ? zoneTolerance : 600.0;
            SelectedReinforcementDirection = statsWindow.SelectedReinforcementDirection;

            UseCutLengths = statsWindow.UseCutLengths;
            RoundIncrement = statsWindow.RoundIncrement;

            List<FiniteElementFace> filteredFaces = null;
            if (selectedColors != null && selectedColors.Any())
            {
                // Преобразуем выбранные цвета в список RGB
                var selectedColorsRgb = selectedColors.Select(sc => new
                {
                    Red = sc.ColorBrush.Color.R,
                    Green = sc.ColorBrush.Color.G,
                    Blue = sc.ColorBrush.Color.B
                }).ToList();


                // Фильтруем finiteElementFaces по выбранным цветам
                filteredFaces = finiteElementFaces
                    .Where(fe => selectedColorsRgb.Any(c =>
                        c.Red == fe.FaceColor.Red &&
                        c.Green == fe.FaceColor.Green &&
                        c.Blue == fe.FaceColor.Blue))
                    .ToList();
            }


            // Преобразуем выбранные цвета в словарь RGB -> (RebarBarType, Spacing)
            var colorToPropertiesMap = selectedColors.ToDictionary(
                sc => new { sc.ColorBrush.Color.R, sc.ColorBrush.Color.G, sc.ColorBrush.Color.B },
                sc => new { RebarType = sc.SelectedRebarType, Spacing = sc.Spacing }
            );

            foreach (var face in finiteElementFaces)
            {
                var colorKey = new { R = face.FaceColor.Red, G = face.FaceColor.Green, B = face.FaceColor.Blue };

                if (colorToPropertiesMap.ContainsKey(colorKey))
                {
                    // Назначаем тип арматуры и шаг
                    face.RebarType = colorToPropertiesMap[colorKey].RebarType;
                    face.Spacing = colorToPropertiesMap[colorKey].Spacing;
                }
            }

            List<List<FiniteElementFace>> groupedFaces = null;
            if (filteredFaces != null && filteredFaces.Any())
            {
                groupedFaces = GroupConnectedFaces(filteredFaces);
            }

            if (SelectedReinforcementDirection == "Низ X" || SelectedReinforcementDirection == "Верх X")
            {
                ProcessXClusters(groupedFaces, doc, doc.ActiveView);
            }
            else if (SelectedReinforcementDirection == "Низ Y" || SelectedReinforcementDirection == "Верх Y")
            {
                ProcessYClusters(groupedFaces, doc, doc.ActiveView);
            }

            return Result.Succeeded;
        }
        private bool AreVerticesEqual(List<XYZ> vertices1, List<XYZ> vertices2)
        {
            if (vertices1.Count != vertices2.Count)
                return false;

            return !vertices1.Except(vertices2, new XYZComparer()).Any();
        }
        private class XYZComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ p1, XYZ p2)
            {
                // Сравнение только по X и Y координатам, округленным до 6 знаков
                return Math.Round(p1.X, 6) == Math.Round(p2.X, 6) &&
                       Math.Round(p1.Y, 6) == Math.Round(p2.Y, 6);
            }

            public int GetHashCode(XYZ obj)
            {
                // Используем только X и Y для хэш-кода
                return (Math.Round(obj.X, 6), Math.Round(obj.Y, 6)).GetHashCode();
            }
        }
        private List<List<FiniteElementFace>> GroupConnectedFaces(List<FiniteElementFace> filteredFaces)
        {
            List<List<FiniteElementFace>> groupedFaces = new List<List<FiniteElementFace>>();
            HashSet<int> processedFaces = new HashSet<int>();

            foreach (var face in filteredFaces)
            {
                if (!processedFaces.Contains(face.Id))
                {
                    // Создаем новую группу
                    List<FiniteElementFace> group = new List<FiniteElementFace>();
                    DFS(face, filteredFaces, processedFaces, group);
                    groupedFaces.Add(group);
                }
            }

            return groupedFaces;
        }
        private void DFS(FiniteElementFace currentFace, List<FiniteElementFace> allFaces, HashSet<int> processedFaces, List<FiniteElementFace> group)
        {
            // Добавляем текущую грань в группу и отмечаем как обработанную
            group.Add(currentFace);
            processedFaces.Add(currentFace.Id);

            // Ищем соседей
            foreach (var face in allFaces)
            {
                if (!processedFaces.Contains(face.Id) && AreFacesConnected(currentFace, face))
                {
                    DFS(face, allFaces, processedFaces, group);
                }
            }
        }
        private bool AreFacesConnected(FiniteElementFace face1, FiniteElementFace face2)
        {
            // Проверяем, есть ли общие вершины, игнорируя Z
            return face1.Vertices.Any(v1 => face2.Vertices.Any(v2 => IsVertexEqual(v1, v2)));
        }
        private bool IsVertexEqual(XYZ v1, XYZ v2)
        {
            return Math.Round(v1.X, 6) == Math.Round(v2.X, 6) &&
                   Math.Round(v1.Y, 6) == Math.Round(v2.Y, 6);
        }


        //Обработка общих кластеров по X
        private void ProcessXClusters(List<List<FiniteElementFace>> groupedFaces, Document doc, View view)
        {
            using (Transaction t = new Transaction(doc, "Зоны усиления"))
            {
                t.Start();

                //Допуск при формировании линий
                double yTolerance = AdjacentElementsTolerance / 304.8;

                foreach (var cluster in groupedFaces)
                {
                    List<LineData> lines = new List<LineData>();
                    HashSet<int> processedIds = new HashSet<int>();

                    while (true)
                    {
                        // Найти самый нижний левый элемент
                        var startFace = cluster
                            .Where(face => !processedIds.Contains(face.Id))
                            .OrderBy(face => face.Vertices.Min(v => v.Y))
                            .ThenBy(face => face.Vertices.Min(v => v.X))
                            .FirstOrDefault();

                        if (startFace == null) break; // Все элементы обработаны

                        // Найти линию элементов
                        List<FiniteElementFace> line = FindXLine(startFace, cluster, processedIds, yTolerance);

                        if (line.Any())
                        {
#if R2019 || R2020 || R2021
                            // Вычисляем площадь армирования для каждого элемента линии
                            var maxReinforcementFace = line
                                .OrderByDescending(face => CalculateReinforcementAreaPerMeter(face.RebarType.BarDiameter, face.Spacing))
                                .First();
                            double maxReinforcementArea = CalculateReinforcementAreaPerMeter(maxReinforcementFace.RebarType.BarDiameter, maxReinforcementFace.Spacing);
#else
                            // Вычисляем площадь армирования для каждого элемента линии
                            var maxReinforcementFace = line
                                .OrderByDescending(face => CalculateReinforcementAreaPerMeter(face.RebarType.BarNominalDiameter, face.Spacing))
                                .First();
                            double maxReinforcementArea = CalculateReinforcementAreaPerMeter(maxReinforcementFace.RebarType.BarNominalDiameter, maxReinforcementFace.Spacing);
#endif

                            Color maxReinforcementColor = maxReinforcementFace.FaceColor;

                            LineData lineData = new LineData
                            {
                                RebarType = maxReinforcementFace.RebarType,
                                Spacing = maxReinforcementFace.Spacing,
                                MinX = line.Min(face => face.Vertices.Min(v => v.X)),
                                MaxX = line.Max(face => face.Vertices.Max(v => v.X)),
                                MinY = line.Min(face => face.Vertices.Min(v => v.Y)),
                                MaxY = line.Max(face => face.Vertices.Max(v => v.Y)),
                                Faces = line
                            };
                            lines.Add(lineData);

                            // Пометить элементы как обработанные
                            foreach (var face in line)
                            {
                                processedIds.Add(face.Id);
                            }
                        }
                    }

                    List<Cluster> clusters = GroupLinesIntoXClusters(lines);
                    CreatePathReinforcementForXClusters(clusters, doc, Floor);
                }

                t.Commit();
            }
        }
        private List<FiniteElementFace> FindXLine(FiniteElementFace startFace, List<FiniteElementFace> cluster, HashSet<int> processedIds, double yTolerance)
        {
            List<FiniteElementFace> line = new List<FiniteElementFace> { startFace };
            HashSet<int> localProcessedIds = new HashSet<int> { startFace.Id };

            while (true)
            {
                // Найти следующий примыкающий элемент справа
                var nextFace = cluster
                    .Where(face => !processedIds.Contains(face.Id) && !localProcessedIds.Contains(face.Id))
                    .Where(face => AreFacesAlignedX(startFace, face, yTolerance))
                    .OrderBy(face => face.Vertices.Min(v => v.X))
                    .FirstOrDefault();

                if (nextFace == null) break; // Нет больше примыкающих элементов

                line.Add(nextFace);
                localProcessedIds.Add(nextFace.Id);
                startFace = nextFace;
            }

            return line;
        }
        private bool AreFacesAlignedX(FiniteElementFace face1, FiniteElementFace face2, double yTolerance)
        {
            // Проверяем, что Y координаты нижних точек находятся в пределах допуска
            double minY1 = face1.Vertices.Min(v => v.Y);
            double minY2 = face2.Vertices.Min(v => v.Y);
            return Math.Abs(minY1 - minY2) <= yTolerance;
        }
        private List<Cluster> GroupLinesIntoXClusters(List<LineData> lines)
        {
            double xTolerance = ZoneMergeTolerance / 304.8; // Допустимое отклонение по X
            List<Cluster> clusters = new List<Cluster>();

            Cluster currentCluster = null;

            foreach (var line in lines) // Порядок lines уже соблюдается
            {
                if (currentCluster == null)
                {
                    // Создаём первый кластер
                    currentCluster = new Cluster
                    {
                        RebarType = line.RebarType,
                        Spacing = line.Spacing,
                        MinX = line.MinX,
                        MaxX = line.MaxX,
                        MinY = line.MinY,
                        MaxY = line.MaxY,
                        Lines = new List<LineData> { line }
                    };
                    clusters.Add(currentCluster);
                }
                else
                {
                    // Сравниваем с первой линией в текущем кластере
                    var firstLine = currentCluster.Lines.First();
                    bool canAddToCluster =
                        Math.Abs(firstLine.MinX - line.MinX) <= xTolerance &&
                        Math.Abs(firstLine.MaxX - line.MaxX) <= xTolerance &&
                        Math.Abs(firstLine.Spacing - line.Spacing) < 0.001 &&
                        firstLine.RebarType.Equals(line.RebarType);

                    if (canAddToCluster)
                    {
                        // Добавляем линию в текущий кластер
                        currentCluster.Lines.Add(line);
                        currentCluster.MinX = Math.Min(currentCluster.MinX, line.MinX);
                        currentCluster.MaxX = Math.Max(currentCluster.MaxX, line.MaxX);
                        currentCluster.MinY = Math.Min(currentCluster.MinY, line.MinY);
                        currentCluster.MaxY = Math.Max(currentCluster.MaxY, line.MaxY);
                    }
                    else
                    {
                        // Завершаем текущий кластер и создаём новый
                        currentCluster = new Cluster
                        {
                            RebarType = line.RebarType,
                            Spacing = line.Spacing,
                            MinX = line.MinX,
                            MaxX = line.MaxX,
                            MinY = line.MinY,
                            MaxY = line.MaxY,
                            Lines = new List<LineData> { line }
                        };
                        clusters.Add(currentCluster);
                    }
                }
            }

            return clusters;
        }
        private void CreatePathReinforcementForXClusters(List<Cluster> clusters, Document doc, Floor hostElement)
        {
            var pathReinforcementType = new FilteredElementCollector(doc)
                .OfClass(typeof(PathReinforcementType))
                .FirstOrDefault();

            if (pathReinforcementType == null)
            {
                TaskDialog.Show("Error", "No Path Reinforcement Type found in the document.");
                return;
            }

            foreach (var cluster in clusters)
            {
                // Пропускаем кластеры с одной линией, содержащей один элемент
                if (cluster.Lines.Count == 1 && cluster.Lines[0].Faces.Count == 1)
                {
                    continue;
                }

                // Находим левую линию кластера
                LineData leftLine = cluster.Lines
                    .OrderBy(line => line.MinX) // Сортируем по минимальной X-координате
                    .FirstOrDefault();

                if (leftLine == null)
                {
                    continue; // Пропускаем, если линия не найдена
                }

                // Получаем класс бетона
                string concreteClass = ConcreteClass; // Параметр класса бетона
#if R2019 || R2020 || R2021
                double barDiameter = UnitUtils.ConvertFromInternalUnits(cluster.RebarType.BarDiameter, DisplayUnitType.DUT_MILLIMETERS);
#else
                double barDiameter = UnitUtils.ConvertFromInternalUnits(cluster.RebarType.BarNominalDiameter, UnitTypeId.Millimeters);
#endif


                Guid steelGradeGuid = new Guid("32a47c7f-e91d-4a8e-bf24-927cb679b4d1");
                double steelGrade = cluster.RebarType.get_Parameter(steelGradeGuid).AsDouble();

                double anchorageLength = CalculateAnchorageLength(barDiameter, concreteClass, steelGrade); // Метод вычисления длины анкеровки

                // Смещаем зону влево на длину анкеровки
                XYZ p1 = new XYZ(cluster.MinX - anchorageLength / 304.8, cluster.MinY, 0);
                XYZ p2 = new XYZ(cluster.MinX - anchorageLength / 304.8, cluster.MaxY, 0);

                // Увеличиваем длину зоны на две длины анкеровки
                double distance = (cluster.MaxX - cluster.MinX) + 2 * anchorageLength / 304.8;

                // Переводим длину в миллиметры
#if R2019 || R2020 || R2021
                double distanceInMillimeters = Math.Round(UnitUtils.ConvertFromInternalUnits(distance, DisplayUnitType.DUT_MILLIMETERS));
#else
                double distanceInMillimeters = Math.Round(UnitUtils.ConvertFromInternalUnits(distance, UnitTypeId.Millimeters));
#endif

                double roundedDistanceInMillimeters = RoundDistance(distanceInMillimeters);

                // Конвертируем округленную длину обратно в футы
#if R2019 || R2020 || R2021
                double roundedDistance = UnitUtils.ConvertToInternalUnits(roundedDistanceInMillimeters, DisplayUnitType.DUT_MILLIMETERS);
#else
                double roundedDistance = UnitUtils.ConvertToInternalUnits(roundedDistanceInMillimeters, UnitTypeId.Millimeters);
#endif

                // Смещаем арматуру на разницу между округленной и исходной длиной
                double offset = (roundedDistance - distance) / 2.0; // Половина разницы

                // Корректируем кривые арматуры перед созданием зоны
                p1 = new XYZ(p1.X - offset, p1.Y, p1.Z);
                p2 = new XYZ(p2.X - offset, p2.Y, p2.Z);

                // Создаём кривые для армирования
                List<Curve> curves = new List<Curve>();
                if (p1.DistanceTo(p2) < 0.00328084) continue;

                curves.Add(Line.CreateBound(p1, p2));

                if (curves.Count == 0)
                {
                    continue; // Пропускаем, если нет валидных кривых
                }

                // Создаём армирование по траектории
                try
                {
                    var pathReinforcement = PathReinforcement.Create(
                        doc,
                        hostElement,
                        curves,
                        flip: false,
                        pathReinforcementTypeId: pathReinforcementType.Id, // Используем тип по умолчанию
                        rebarBarTypeId: cluster.RebarType.Id, // Тип арматуры из кластера
                        startRebarHookTypeId: ElementId.InvalidElementId, // Без крюков
                        endRebarHookTypeId: ElementId.InvalidElementId  // Без крюков
                    );

                    if (pathReinforcement != null)
                    {
                        // Устанавливаем PATH_REIN_FACE_SLAB = 1
                        Parameter faceSlabParameter = pathReinforcement.get_Parameter(BuiltInParameter.PATH_REIN_FACE_SLAB);
                        if (faceSlabParameter != null && !faceSlabParameter.IsReadOnly)
                        {
                            if (SelectedReinforcementDirection == "Низ X")
                            {
                                faceSlabParameter.Set(1);
                            }
                            else
                            {
                                faceSlabParameter.Set(0);
                            }
                        }

                        // Устанавливаем шаг арматры
                        Parameter spacingParameter = pathReinforcement.get_Parameter(BuiltInParameter.PATH_REIN_SPACING);
                        if (spacingParameter != null && !spacingParameter.IsReadOnly)
                        {
#if R2019 || R2020 || R2021
                            spacingParameter.Set(UnitUtils.ConvertToInternalUnits(cluster.Spacing, DisplayUnitType.DUT_MILLIMETERS));
#else
                            spacingParameter.Set(UnitUtils.ConvertToInternalUnits(cluster.Spacing, UnitTypeId.Millimeters));
#endif
                        }

                        // Устанавливаем длину арматуры
                        Parameter lengthParameter = pathReinforcement.get_Parameter(BuiltInParameter.PATH_REIN_LENGTH_1);
                        if (lengthParameter != null && !lengthParameter.IsReadOnly)
                        {
                            lengthParameter.Set(roundedDistance);
                        }
                    }
                }
                catch
                {
                    //Пропуск
                }
            }
        }


        // Обработка общих кластеров по Y
        private void ProcessYClusters(List<List<FiniteElementFace>> groupedFaces, Document doc, View view)
        {
            using (Transaction t = new Transaction(doc, "Зоны усиления"))
            {
                t.Start();

                // Допуск при формировании линий
                double xTolerance = AdjacentElementsTolerance / 304.8;

                foreach (var cluster in groupedFaces)
                {
                    List<LineData> lines = new List<LineData>();
                    HashSet<int> processedIds = new HashSet<int>();

                    while (true)
                    {
                        // Найти самый левый нижний элемент
                        var startFace = cluster
                            .Where(face => !processedIds.Contains(face.Id))
                            .OrderBy(face => face.Vertices.Min(v => v.X))
                            .ThenBy(face => face.Vertices.Min(v => v.Y))
                            .FirstOrDefault();

                        if (startFace == null) break; // Все элементы обработаны

                        // Найти линию элементов вдоль оси Y
                        List<FiniteElementFace> line = FindYLine(startFace, cluster, processedIds, xTolerance);

                        if (line.Any())
                        {
#if R2019 || R2020 || R2021
                            // Вычисляем площадь армирования для каждого элемента линии
                            var maxReinforcementFace = line
                                .OrderByDescending(face => CalculateReinforcementAreaPerMeter(face.RebarType.BarDiameter, face.Spacing))
                                .First();
                            double maxReinforcementArea = CalculateReinforcementAreaPerMeter(maxReinforcementFace.RebarType.BarDiameter, maxReinforcementFace.Spacing);
#else
                            // Вычисляем площадь армирования для каждого элемента линии
                            var maxReinforcementFace = line
                                .OrderByDescending(face => CalculateReinforcementAreaPerMeter(face.RebarType.BarNominalDiameter, face.Spacing))
                                .First();
                            double maxReinforcementArea = CalculateReinforcementAreaPerMeter(maxReinforcementFace.RebarType.BarNominalDiameter, maxReinforcementFace.Spacing);
#endif
                            Color maxReinforcementColor = maxReinforcementFace.FaceColor;

                            LineData lineData = new LineData
                            {
                                RebarType = maxReinforcementFace.RebarType,
                                Spacing = maxReinforcementFace.Spacing,
                                MinX = line.Min(face => face.Vertices.Min(v => v.X)),
                                MaxX = line.Max(face => face.Vertices.Max(v => v.X)),
                                MinY = line.Min(face => face.Vertices.Min(v => v.Y)),
                                MaxY = line.Max(face => face.Vertices.Max(v => v.Y)),
                                Faces = line
                            };
                            lines.Add(lineData);

                            // Пометить элементы как обработанные
                            foreach (var face in line)
                            {
                                processedIds.Add(face.Id);
                            }
                        }
                    }

                    List<Cluster> clusters = GroupLinesIntoYClusters(lines);
                    CreatePathReinforcementForYClusters(clusters, doc, Floor);
                }

                t.Commit();
            }
        }
        private List<FiniteElementFace> FindYLine(FiniteElementFace startFace, List<FiniteElementFace> cluster, HashSet<int> processedIds, double xTolerance)
        {
            List<FiniteElementFace> line = new List<FiniteElementFace> { startFace };
            HashSet<int> localProcessedIds = new HashSet<int> { startFace.Id };

            while (true)
            {
                // Найти следующий примыкающий элемент сверху
                var nextFace = cluster
                    .Where(face => !processedIds.Contains(face.Id) && !localProcessedIds.Contains(face.Id))
                    .Where(face => AreFacesAlignedY(startFace, face, xTolerance))
                    .OrderBy(face => face.Vertices.Min(v => v.Y))
                    .FirstOrDefault();

                if (nextFace == null) break; // Нет больше примыкающих элементов

                line.Add(nextFace);
                localProcessedIds.Add(nextFace.Id);
                startFace = nextFace;
            }

            return line;
        }
        private bool AreFacesAlignedY(FiniteElementFace face1, FiniteElementFace face2, double xTolerance)
        {
            // Проверяем, что X координаты нижних точек находятся в пределах допуска
            double minX1 = face1.Vertices.Min(v => v.X);
            double minX2 = face2.Vertices.Min(v => v.X);
            return Math.Abs(minX1 - minX2) <= xTolerance;
        }
        private List<Cluster> GroupLinesIntoYClusters(List<LineData> lines)
        {
            double yTolerance = ZoneMergeTolerance / 304.8; // Допустимое отклонение по Y
            List<Cluster> clusters = new List<Cluster>();

            Cluster currentCluster = null;

            foreach (var line in lines) // Порядок lines уже соблюдается
            {
                if (currentCluster == null)
                {
                    // Создаём первый кластер
                    currentCluster = new Cluster
                    {
                        RebarType = line.RebarType,
                        Spacing = line.Spacing,
                        MinX = line.MinX,
                        MaxX = line.MaxX,
                        MinY = line.MinY,
                        MaxY = line.MaxY,
                        Lines = new List<LineData> { line }
                    };
                    clusters.Add(currentCluster);
                }
                else
                {
                    // Сравниваем с первой линией в текущем кластере
                    var firstLine = currentCluster.Lines.First();
                    bool canAddToCluster =
                        Math.Abs(firstLine.MinY - line.MinY) <= yTolerance &&
                        Math.Abs(firstLine.MaxY - line.MaxY) <= yTolerance &&
                        Math.Abs(firstLine.Spacing - line.Spacing) < 0.001 &&
                        firstLine.RebarType.Equals(line.RebarType);

                    if (canAddToCluster)
                    {
                        // Добавляем линию в текущий кластер
                        currentCluster.Lines.Add(line);
                        currentCluster.MinX = Math.Min(currentCluster.MinX, line.MinX);
                        currentCluster.MaxX = Math.Max(currentCluster.MaxX, line.MaxX);
                        currentCluster.MinY = Math.Min(currentCluster.MinY, line.MinY);
                        currentCluster.MaxY = Math.Max(currentCluster.MaxY, line.MaxY);
                    }
                    else
                    {
                        // Завершаем текущий кластер и создаём новый
                        currentCluster = new Cluster
                        {
                            RebarType = line.RebarType,
                            Spacing = line.Spacing,
                            MinX = line.MinX,
                            MaxX = line.MaxX,
                            MinY = line.MinY,
                            MaxY = line.MaxY,
                            Lines = new List<LineData> { line }
                        };
                        clusters.Add(currentCluster);
                    }
                }
            }

            return clusters;
        }
        private void CreatePathReinforcementForYClusters(List<Cluster> clusters, Document doc, Floor hostElement)
        {
            var pathReinforcementType = new FilteredElementCollector(doc)
                .OfClass(typeof(PathReinforcementType))
                .FirstOrDefault();

            if (pathReinforcementType == null)
            {
                TaskDialog.Show("Error", "No Path Reinforcement Type found in the document.");
                return;
            }

            foreach (var cluster in clusters)
            {
                // Пропускаем кластеры с одной линией, содержащей один элемент
                if (cluster.Lines.Count == 1 && cluster.Lines[0].Faces.Count == 1)
                {
                    continue;
                }

                // Находим верхнюю линию кластера
                LineData topLine = cluster.Lines
                    .OrderByDescending(line => line.MaxY) // Сортируем по максимальной Y-координате
                    .FirstOrDefault();

                if (topLine == null)
                {
                    continue; // Пропускаем, если линия не найдена
                }

                // Получаем класс бетона
                string concreteClass = ConcreteClass; // Параметр класса бетона
#if R2019 || R2020 || R2021
                double barDiameter = UnitUtils.ConvertFromInternalUnits(cluster.RebarType.BarDiameter, DisplayUnitType.DUT_MILLIMETERS);
#else
        double barDiameter = UnitUtils.ConvertFromInternalUnits(cluster.RebarType.BarNominalDiameter, UnitTypeId.Millimeters);
#endif

                Guid steelGradeGuid = new Guid("32a47c7f-e91d-4a8e-bf24-927cb679b4d1");
                double steelGrade = cluster.RebarType.get_Parameter(steelGradeGuid).AsDouble();

                double anchorageLength = CalculateAnchorageLength(barDiameter, concreteClass, steelGrade); // Метод вычисления длины анкеровки

                // Определяем границы зоны по оси Y
                XYZ p1 = new XYZ(cluster.MinX, cluster.MaxY + anchorageLength / 304.8, 0);
                XYZ p2 = new XYZ(cluster.MaxX, cluster.MaxY + anchorageLength / 304.8, 0);

                // Увеличиваем длину зоны на две длины анкеровки
                double distance = (cluster.MaxY - cluster.MinY) + 2 * anchorageLength / 304.8;

                // Переводим длину в миллиметры и округляем
#if R2019 || R2020 || R2021
                double distanceInMillimeters = Math.Round(UnitUtils.ConvertFromInternalUnits(distance, DisplayUnitType.DUT_MILLIMETERS));
#else
        double distanceInMillimeters = Math.Round(UnitUtils.ConvertFromInternalUnits(distance, UnitTypeId.Millimeters));
#endif

                double roundedDistanceInMillimeters = RoundDistance(distanceInMillimeters);

                // Конвертируем округленную длину обратно в футы
#if R2019 || R2020 || R2021
                double roundedDistance = UnitUtils.ConvertToInternalUnits(roundedDistanceInMillimeters, DisplayUnitType.DUT_MILLIMETERS);
#else
        double roundedDistance = UnitUtils.ConvertToInternalUnits(roundedDistanceInMillimeters, UnitTypeId.Millimeters);
#endif

                // Корректируем положение арматуры с учётом разницы между округленной и исходной длиной
                double offset = (roundedDistance - distance) / 2.0;

                p1 = new XYZ(p1.X, p1.Y + offset, p1.Z);
                p2 = new XYZ(p2.X, p2.Y + offset, p2.Z);

                // Создаём кривые для армирования
                List<Curve> curves = new List<Curve>
        {
            Line.CreateBound(p1, p2)
        };

                if (curves.Count == 0)
                {
                    continue; // Пропускаем, если нет валидных кривых
                }

                // Создаём армирование по траектории
                try
                {
                    var pathReinforcement = PathReinforcement.Create(
                        doc,
                        hostElement,
                        curves,
                        flip: false,
                        pathReinforcementTypeId: pathReinforcementType.Id, // Используем тип по умолчанию
                        rebarBarTypeId: cluster.RebarType.Id, // Тип арматуры из кластера
                        startRebarHookTypeId: ElementId.InvalidElementId, // Без крюков
                        endRebarHookTypeId: ElementId.InvalidElementId  // Без крюков
                    );

                    if (pathReinforcement != null)
                    {
                        // Устанавливаем PATH_REIN_FACE_SLAB = 1
                        Parameter faceSlabParameter = pathReinforcement.get_Parameter(BuiltInParameter.PATH_REIN_FACE_SLAB);
                        if (faceSlabParameter != null && !faceSlabParameter.IsReadOnly)
                        {
                            if (SelectedReinforcementDirection == "Низ Y")
                            {
                                faceSlabParameter.Set(1);
                            }
                            else
                            {
                                faceSlabParameter.Set(0);
                            }
                        }

                        // Устанавливаем шаг арматуры
                        Parameter spacingParameter = pathReinforcement.get_Parameter(BuiltInParameter.PATH_REIN_SPACING);
                        if (spacingParameter != null && !spacingParameter.IsReadOnly)
                        {
#if R2019 || R2020 || R2021
                            spacingParameter.Set(UnitUtils.ConvertToInternalUnits(cluster.Spacing, DisplayUnitType.DUT_MILLIMETERS));
#else
                    spacingParameter.Set(UnitUtils.ConvertToInternalUnits(cluster.Spacing, UnitTypeId.Millimeters));
#endif
                        }

                        // Устанавливаем длину арматуры
                        Parameter lengthParameter = pathReinforcement.get_Parameter(BuiltInParameter.PATH_REIN_LENGTH_1);
                        if (lengthParameter != null && !lengthParameter.IsReadOnly)
                        {
                            lengthParameter.Set(roundedDistance);
                        }
                    }
                }
                catch
                {
                    //Пропуск
                }
            }
        }

        private double CalculateReinforcementAreaPerMeter(double diameterFeet, double spacingMm)
        {
            // Преобразуем диаметр из футов в сантиметры
            double diameterCm = diameterFeet * 30.48; // 1 фут = 30.48 см

            // Преобразуем шаг из миллиметров в сантиметры
            double spacingCm = spacingMm / 10.0; // 1 мм = 0.1 см

            // Рассчитываем площадь одного стержня (см²)
            double areaPerBar = Math.PI * Math.Pow(diameterCm, 2) / 4;

            // Рассчитываем площадь армирования на метр (см²/м)
            return areaPerBar / spacingCm;
        }
        private double CalculateAnchorageLength(double barDiameter, string concreteClass, double steelGrade)
        {
            // Данные для анкеровки
            var anchorageLengths = new Dictionary<string, Dictionary<double, Dictionary<double, double>>>
            {
                {
                    "B15", new Dictionary<double, Dictionary<double, double>>
                    {
                        { 240, new Dictionary<double, double>
                            {
                                { 6, 290 }, { 8, 390 }, { 10, 480 }, { 12, 580 }, { 14, 670 },
                                { 16, 770 }, { 18, 860 }, { 20, 960 }, { 22, 1060 }, { 25, 1200 },
                                { 28, 1340 }, { 32, 1530 }, { 36, 1920 } , { 40, 2130 }
                            }
                        },
                        { 400, new Dictionary<double, double>
                            {
                                { 6, 290 }, { 8, 380 }, { 10, 480 }, { 12, 570 }, { 14, 670 },
                                { 16, 760 }, { 18, 860 }, { 20, 950 }, { 22, 1050 }, { 25, 1190 },
                                { 28, 1330 }, { 32, 1520 }, { 36, 1900 }, { 40, 2110 }
                            }
                        },
                        { 500, new Dictionary<double, double>
                            {
                                { 6, 350 }, { 8, 470 }, { 10, 580 }, { 12, 700 }, { 14, 820 },
                                { 16, 930 }, { 18, 1050 }, { 20, 1160 }, { 22, 1280 }, { 25, 1450 },
                                { 28, 1630 }, { 32, 1860 }, { 36, 2320 }, { 40, 2580 }
                            }
                        }
                    }
                },
                {
                    "B20", new Dictionary<double, Dictionary<double, double>>
                    {
                        { 240, new Dictionary<double, double>
                            {
                                { 6, 240 }, { 8, 320 }, { 10, 400 }, { 12, 480 }, { 14, 560 },
                                { 16, 640 }, { 18, 720 }, { 20, 800 }, { 22, 880 }, { 25, 1000 },
                                { 28, 1120 }, { 32, 1280 }, { 36, 1600 }, { 40, 1770 }
                            }
                        },
                        { 400, new Dictionary<double, double>
                            {
                                { 6, 240 }, { 8, 320 }, { 10, 400 }, { 12, 480 }, { 14, 560 },
                                { 16, 640 }, { 18, 710 }, { 20, 790 }, { 22, 870 }, { 25, 990 },
                                { 28, 1110 }, { 32, 1270}, { 36, 1580 }, { 40, 1760 }
                            }
                        },
                        { 500, new Dictionary<double, double>
                            {
                                { 6, 290 }, { 8, 390 }, { 10, 490 }, { 12, 580 }, { 14, 680 },
                                { 16, 780 }, { 18, 870 }, { 20, 970 }, { 22, 1070 }, { 25, 1210 },
                                { 28, 1360 }, { 32, 1550 }, { 36, 1940 }, { 40, 2150 }
                            }
                        }
                    }
                },
                {
                    "B25", new Dictionary<double, Dictionary<double, double>>
                    {
                        { 240, new Dictionary<double, double>
                            {
                                { 6, 210 }, { 8, 280 }, { 10, 350 }, { 12, 410 }, { 14, 480 },
                                { 16, 550 }, { 18, 620 }, { 20, 690 }, { 22, 750 }, { 25, 860 },
                                { 28, 960 }, { 32, 1100 }, { 36, 1370 }, { 40, 1520 }
                            }
                        },
                        { 400, new Dictionary<double, double>
                            {
                                { 6, 210 }, { 8, 270 }, { 10, 340 }, { 12, 410 }, { 14, 480 },
                                { 16, 540 }, { 18, 610 }, { 20, 680 }, { 22, 750 }, { 25, 850 },
                                { 28, 950 }, { 32, 1090 }, { 36, 1360 }, { 40, 1510 }
                            }
                        },
                        { 500, new Dictionary<double, double>
                            {
                                { 6, 250 }, { 8, 340 }, { 10, 420 }, { 12, 500 }, { 14, 580 },
                                { 16, 670 }, { 18, 750 }, { 20, 830 }, { 22, 920 }, { 25, 1040 },
                                { 28, 1160 }, { 32, 1330 }, { 36, 1660 }, { 40, 1850 }
                            }
                        }
                    }
                },
                {
                    "B30", new Dictionary<double, Dictionary<double, double>>
                    {
                        { 240, new Dictionary<double, double>
                            {
                                { 6, 200 }, { 8, 250 }, { 10, 320 }, { 12, 380 }, { 14, 440 },
                                { 16, 500 }, { 18, 560 }, { 20, 630 }, { 22, 690 }, { 25, 780 },
                                { 28, 880 }, { 32, 1000 }, { 36, 1250 }, { 40, 1390 }
                            }
                        },
                        { 400, new Dictionary<double, double>
                            {
                                { 6, 200 }, { 8, 250 }, { 10, 310 }, { 12, 370 }, { 14, 440 },
                                { 16, 480 }, { 18, 560 }, { 20, 620 }, { 22, 680 }, { 25, 780 },
                                { 28, 870 }, { 32, 990 }, { 36, 1240 }, { 40, 1380 }
                            }
                        },
                        { 500, new Dictionary<double, double>
                            {
                                { 6, 230 }, { 8, 310 }, { 10, 380 }, { 12, 460 }, { 14, 530 },
                                { 16, 610 }, { 18, 680 }, { 20, 760 }, { 22, 840 }, { 25, 950 },
                                { 28, 1060 }, { 30, 1210 }, { 32, 1520 }, { 36, 1690 }, { 40, 1690 }
                            }
                        }
                    }
                }
            };

            // Проверяем наличие данных для указанного класса бетона
            if (!anchorageLengths.ContainsKey(concreteClass))
                throw new ArgumentException($"Данные для класса бетона {concreteClass} отсутствуют.");

            var steelData = anchorageLengths[concreteClass];

            // Проверяем наличие данных для указанной марки стали
            if (!steelData.ContainsKey(steelGrade))
            {
                // Поиск ближайшего значения для steelGrade
                double closestSteelGrade = steelData.Keys
                    .OrderBy(key => Math.Abs(key - steelGrade))
                    .FirstOrDefault();

                if (Math.Abs(closestSteelGrade - steelGrade) < 0.0001) // Допустимая точность
                {
                    steelGrade = closestSteelGrade;
                }
                else
                {
                    throw new ArgumentException($"Данные для марки стали {steelGrade} отсутствуют.");
                }
            }

            var diameterData = steelData[steelGrade];

            // Проверяем наличие данных для указанного диаметра
            if (!diameterData.ContainsKey(barDiameter))
            {
                // Поиск ближайшего значения для barDiameter
                double closestBarDiameter = diameterData.Keys
                    .OrderBy(key => Math.Abs(key - barDiameter))
                    .FirstOrDefault();

                if (Math.Abs(closestBarDiameter - barDiameter) < 0.0001) // Допустимая точность
                {
                    barDiameter = closestBarDiameter;
                }
                else
                {
                    throw new ArgumentException($"Данные для диаметра {barDiameter} отсутствуют.");
                }
            }

            // Возвращаем длину анкеровки
            return diameterData[barDiameter];
        }
        private double RoundDistance(double distanceInMm)
        {
            if (UseCutLengths)
            {
                // Нарезка длин
                List<double> cutLengths = new List<double> { 9750, 8775, 7800, 6825, 5850, 4875, 3900, 2925, 2340, 1950, 1670, 1460, 1300, 1170 };
                foreach (var length in cutLengths.OrderBy(l => l))
                {
                    if (distanceInMm <= length)
                        return length;
                }
                // Округление по инкременту
                double increment = double.TryParse(RoundIncrement, out double parsedIncrement) ? parsedIncrement : 10.0;
                double roundedDistanceMm = Math.Ceiling(distanceInMm / increment) * increment;
                return roundedDistanceMm;
            }
            else
            {
                // Округление по инкременту
                double increment = double.TryParse(RoundIncrement, out double parsedIncrement) ? parsedIncrement : 10.0;
                double roundedDistanceMm = Math.Ceiling(distanceInMm / increment) * increment;
                return roundedDistanceMm;
            }
        }

        private static async Task GetPluginStartInfo()
        {
            // Получаем сборку, в которой выполняется текущий код
            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            string assemblyName = "SlabReinforcement";
            string assemblyNameRus = "Зоны усиления плит";
            string assemblyFolderPath = Path.GetDirectoryName(thisAssembly.Location);

            int lastBackslashIndex = assemblyFolderPath.LastIndexOf("\\");
            string dllPath = assemblyFolderPath.Substring(0, lastBackslashIndex + 1) + "PluginInfoCollector\\PluginInfoCollector.dll";

            Assembly assembly = Assembly.LoadFrom(dllPath);
            Type type = assembly.GetType("PluginInfoCollector.InfoCollector");

            if (type != null)
            {
                // Создание экземпляра класса
                object instance = Activator.CreateInstance(type);

                // Получение метода CollectPluginUsageAsync
                var method = type.GetMethod("CollectPluginUsageAsync");

                if (method != null)
                {
                    // Вызов асинхронного метода через reflection
                    Task task = (Task)method.Invoke(instance, new object[] { assemblyName, assemblyNameRus });
                    await task;  // Ожидание завершения асинхронного метода
                }
            }
        }
    }
}
