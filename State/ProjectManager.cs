using System.IO;
using Newtonsoft.Json;
using TapSynth.Sequencing;

namespace TapSynth.State
{
    public class ProjectManager
    {
        public Pattern[] Patterns { get; set; } = new Pattern[64];

        public ProjectManager()
        {
            for (int i = 0; i < 64; i++)
                Patterns[i] = new Pattern();
        }

        public void SaveToFile(string filePath)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static ProjectManager LoadFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<ProjectManager>(json);
            }
            return new ProjectManager();
        }
    }
}
