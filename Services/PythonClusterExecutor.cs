using Python.Runtime;
using Newtonsoft.Json;

namespace WebApplication1.Services
{
    public class PythonClusterExecutor
    {
        private readonly string _pythonHome;

        public PythonClusterExecutor(IConfiguration config)
        {
            _pythonHome = config["Python:Path"];
        }

        public string Execute(object request)
        {
            string json = JsonConvert.SerializeObject(request);

            Environment.SetEnvironmentVariable("PYTHONHOME", _pythonHome);
            Environment.SetEnvironmentVariable(
                "PYTHONPATH",
                $"{_pythonHome};{Directory.GetCurrentDirectory()}/PythonCluster"
            );

            using (Py.GIL())
            {
                dynamic module = Py.Import("ndvi_entry");
                dynamic result = module.ndvi_cluster(json);
                return (string)result;
            }
        }
    }
}
