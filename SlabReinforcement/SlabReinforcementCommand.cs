using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SlabReinforcement
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class SlabReinforcementCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            Selection sel = commandData.Application.ActiveUIDocument.Selection;

            //Выбор плит для армирования
            FloorSelectionFilter selFilter = new FloorSelectionFilter();
            Reference selSlab = sel.PickObject(ObjectType.Element, selFilter, "Выберите плиту!");
            Floor floor = doc.GetElement(selSlab) as Floor;

            ImportInstanceSelectionFilter importInstanceSelectionFilter = new ImportInstanceSelectionFilter();
            Reference selImportInstance = sel.PickObject(ObjectType.Element, importInstanceSelectionFilter, "Выберите DWG подложку!");
            ImportInstance importInstance = doc.GetElement(selImportInstance) as ImportInstance;
            Transform transform = importInstance.GetTransform();

            IList<PointColorItem> pointColorItemList = new List<PointColorItem>();
            Options opt = commandData.Application.ActiveUIDocument.Document.Application.Create.NewGeometryOptions();
            opt.ComputeReferences = true;
            opt.IncludeNonVisibleObjects = true;

            Type typecontroller = typeof(PolyLine);
            PropertyInfo fieldInfo = typecontroller.GetProperty("InternalMaterialElement", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty);

            GeometryElement geoElem1 = importInstance.get_Geometry(opt);
            if (geoElem1 != null)
            {
                foreach (GeometryObject geoObj1 in geoElem1)
                {
                    GeometryInstance geoInst = geoObj1 as GeometryInstance;
                    if (geoInst != null)
                    {
                        GeometryElement geoElem2 = geoInst.GetInstanceGeometry(transform) as GeometryElement;
                        if (geoElem2 != null)
                        {
                            foreach (GeometryObject geoObj2 in geoElem2)
                            {
                                if (geoObj2 is PolyLine)
                                {
                                    Material mat = fieldInfo.GetValue(geoObj2 as PolyLine) as Material;
                                    Color colour = mat.Color;
                                    foreach (XYZ point in (geoObj2 as PolyLine).GetCoordinates())
                                    {
                                        pointColorItemList.Add(new PointColorItem(point, colour));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //Список типов арматуры
            List<RebarBarType> rebarBarTypesList = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(rbt => rbt.Name, new AlphanumComparatorFastString())
                .ToList();
            if (rebarBarTypesList.Count == 0)
            {
                TaskDialog.Show("Revit", "В вашем проекте отсутствует Несущая арматура. Используйте шаблон КЖ.");
                return Result.Cancelled;
            }
            //Формы
            List<RebarShape> rebarShapeList = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(rs => rs.Name, new AlphanumComparatorFastString())
                .ToList();
            if (rebarShapeList.Count == 0)
            {
                TaskDialog.Show("Revit", "В вашем проекте отсутствуют Формы арматурных стержней. Используйте шаблон КЖ.");
                return Result.Cancelled;
            }
            //Список типов арматуры по траектории
            List<PathReinforcementType> pathReinforcementTypeList = new FilteredElementCollector(doc)
                .OfClass(typeof(PathReinforcementType))
                .WhereElementIsElementType()
                .Cast<PathReinforcementType>()
                .OrderBy(rbt => rbt.Name, new AlphanumComparatorFastString())
                .ToList();
            if (pathReinforcementTypeList.Count == 0)
            {
                TaskDialog.Show("Revit", "В вашем проекте отсутствуют типы армирования по траектории.");
                return Result.Cancelled;
            }
            ElementId defaultHookTypeId = ElementId.InvalidElementId;

            pointColorItemList = pointColorItemList.Distinct(new PointColorItemComparer()).ToList();
            SlabReinforcementWPF slabReinforcementWPF = new SlabReinforcementWPF(pointColorItemList, rebarBarTypesList, rebarShapeList);
            slabReinforcementWPF.ShowDialog();
            if (slabReinforcementWPF.DialogResult != true)
            {
                return Result.Cancelled;
            }
            List<PathRebarItem> pathRebarItems = slabReinforcementWPF.PathRebarItems.ToList();
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Армирование плит");
                foreach (PathRebarItem pathRebarItem in pathRebarItems)
                {
                    Curve curve = Line.CreateBound(new XYZ(pathRebarItem.MaxX, pathRebarItem.MaxY, 0), new XYZ(pathRebarItem.MaxX, pathRebarItem.MinY, 0)) as Curve;
                    List<Curve> curves = new List<Curve>() { curve };
                    PathReinforcement.Create(doc
                        , floor
                        , curves
                        , false
                        , pathReinforcementTypeList.First().Id
                        , pathRebarItem.RebarBarType.Id
                        , defaultHookTypeId
                        , defaultHookTypeId);
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
