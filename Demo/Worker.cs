using System;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Demo
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private FileSystemWatcher _folderWatcher;
        private readonly string _inputFolder;
        private readonly IServiceProvider _services;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> settings, IServiceProvider services)
        {
            _logger = logger;
            _services = services;
            _inputFolder = settings.Value.InputFolder;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.CompletedTask;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service Starting");
            if (!Directory.Exists(_inputFolder))
            {
                _logger.LogWarning($"Please make sure the InputFolder [{_inputFolder}] exists, then restart the service.");
                return Task.CompletedTask;
            }

            _logger.LogInformation($"Binding Events from Input Folder: {_inputFolder}");
            _folderWatcher = new FileSystemWatcher(_inputFolder, "*.XML")
            {
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName |
                                  NotifyFilters.DirectoryName
            };
            _folderWatcher.Created += Input_OnChanged;
            _folderWatcher.EnableRaisingEvents = true;

            return base.StartAsync(cancellationToken);
        }

        protected void Input_OnChanged(object source, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                _logger.LogInformation($"InBound Change Event Triggered by [{e.FullPath}]");

                if (File.Exists(e.FullPath))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(e.FullPath);
                    XmlNodeList nodeList = xmlDoc.GetElementsByTagName("dataType");
                    foreach (XmlNode test in nodeList)
                    {
                        if (test.InnerXml == "GasCapacity")
                        {
                            FileInfo file = new FileInfo(e.FullPath);
                            if (File.Exists(ConfigurationManager.AppSettings["DestinationFolder"] + file.Name))
                            {
                                File.Delete(ConfigurationManager.AppSettings["DestinationFolder"] + file.Name);
                            }
                            File.Move(e.FullPath, ConfigurationManager.AppSettings["DestinationFolder"] + file.Name);
                        }
                    }
                }

                _logger.LogInformation("Done with Inbound Change Event");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Service");
            _folderWatcher.EnableRaisingEvents = false;
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _logger.LogInformation("Disposing Service");
            _folderWatcher.Dispose();
            base.Dispose();
        }
    }
}
