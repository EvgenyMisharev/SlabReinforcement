using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SlabReinforcement
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class SlabReinforcementCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            Selection sel = commandData.Application.ActiveUIDocument.Selection;

            var importInstance = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .FirstElement() as ImportInstance;
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
            pointColorItemList = pointColorItemList.Distinct(new PointColorItemComparer()).ToList();
            SlabReinforcementWPF slabReinforcementWPF = new SlabReinforcementWPF(pointColorItemList);
            slabReinforcementWPF.ShowDialog();
            if (slabReinforcementWPF.DialogResult != true)
            {
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }
    }
}
