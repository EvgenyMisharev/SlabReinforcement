using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SlabReinforcement
{
    public class SlabReinforcementSettings
    {
        public string ConcreteClass { get; set; }
        public string AdjacentElementsTolerance { get; set; }
        public string ZoneMergeTolerance { get; set; }
        public string SelectedReinforcementDirection { get; set; }
        public bool UseCutLengths { get; set; }
        public string RoundIncrement { get; set; }
        public List<ColorReinforcementSettings> ColorSettings { get; set; } = new List<ColorReinforcementSettings>();

        private static string GetSettingsFilePath()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(assemblyPath), "SlabReinforcementSettings.xml");
        }

        public static SlabReinforcementSettings LoadSettings()
        {
            string filePath = GetSettingsFilePath();
            if (File.Exists(filePath))
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(SlabReinforcementSettings));
                    return (SlabReinforcementSettings)serializer.Deserialize(fs);
                }
            }
            return new SlabReinforcementSettings
            {
                ConcreteClass = "B20",
                AdjacentElementsTolerance = "50",
                ZoneMergeTolerance = "600",
                SelectedReinforcementDirection = "Низ X",
                UseCutLengths = false,
                RoundIncrement = "10"
            };
        }

        public void SaveSettings()
        {
            string filePath = GetSettingsFilePath();
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SlabReinforcementSettings));
                serializer.Serialize(fs, this);
            }
        }
    }
}
