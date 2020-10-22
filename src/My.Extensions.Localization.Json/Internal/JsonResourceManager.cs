﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace My.Extensions.Localization.Json.Internal
{
    public class JsonResourceManager
    {
        private ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _resourcesCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

        public JsonResourceManager(string resourcesPath, string resourceName = null)
        {
            ResourcesPath = resourcesPath;
            ResourceName = resourceName;
        }

        public string ResourceName { get; }

        public string ResourcesPath { get; }

        public string ResourcesFilePath { get; private set; }

        public virtual ConcurrentDictionary<string, string> GetResourceSet(CultureInfo culture, bool tryParents)
        {
            TryLoadResourceSet(culture);

            if(!_resourcesCache.ContainsKey(culture.Name))
            {
                return null;
            }

            if (tryParents)
            {
                var allResources = new ConcurrentDictionary<string, string>();
                do
                {
                    if(_resourcesCache.TryGetValue(culture.Name, out ConcurrentDictionary<string, string> resources))
                    {
                        foreach (var entry in resources)
                        {
                            allResources.TryAdd(entry.Key, entry.Value);
                        }
                    }

                    culture = culture.Parent;
                } while (culture != CultureInfo.InvariantCulture);

                return allResources;
            }
            else
            {
                _resourcesCache.TryGetValue(culture.Name, out ConcurrentDictionary<string, string> resources);

                return resources;
            }
        }

        public virtual string GetString(string name)
        {
            var culture = CultureInfo.CurrentUICulture;
            GetResourceSet(culture, tryParents: true);

            if (_resourcesCache.Count == 0)
            {
                return null;
            }

            do
            {
                if (_resourcesCache.ContainsKey(culture.Name))
                {
                    if (_resourcesCache[culture.Name].TryGetValue(name, out string value))
                    {
                        return value;
                    }
                }

                culture = culture.Parent;
            } while (culture != culture.Parent);

            return null;
        }

        public virtual string GetString(string name, CultureInfo culture)
        {
            GetResourceSet(culture, tryParents: true);

            if (_resourcesCache.Count == 0)
            {
                return null;
            }

            if (!_resourcesCache.ContainsKey(culture.Name))
            {
                return null;
            }

            return _resourcesCache[culture.Name].TryGetValue(name, out string value)
                ? value
                : null;
        }

        private void TryLoadResourceSet(CultureInfo culture)
        {
            if (string.IsNullOrEmpty(ResourceName))
            {
                var file = Path.Combine(ResourcesPath, $"{culture.Name}.json");
                var resources = LoadJsonResources(file);

                _resourcesCache.TryAdd(culture.Name, new ConcurrentDictionary<string, string>(resources.ToDictionary(r => r.Key, r => r.Value)));
            }
            else
            {
                IEnumerable<string> resourceFiles = null;
                var rootCulture = culture.Name.Substring(0, 2);
                if (ResourceName.Contains("."))
                {
                    resourceFiles = Directory.EnumerateFiles(ResourcesPath, $"{ResourceName}.{rootCulture}*.json");

                    if (resourceFiles.Count() == 0)
                    {
                        resourceFiles = Directory.EnumerateFiles(ResourcesPath, $"{ResourceName.Replace('.', Path.AltDirectorySeparatorChar)}.{rootCulture}*.json");
                    }              
                }
                else
                {
                    resourceFiles = Directory.EnumerateFiles(ResourcesPath, $"{ResourceName}.{rootCulture}*.json");
                }

                foreach (var file in resourceFiles)
                {
                    var resources = LoadJsonResources(file);
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var cultureName = fileName.Substring(fileName.LastIndexOf(".") + 1);
                    
                    culture = CultureInfo.GetCultureInfo(cultureName);
                    
                    if (_resourcesCache.ContainsKey(culture.Name))
                    {
                        foreach (var resource in resources)
                        {
                            _resourcesCache[culture.Name].TryAdd(resource.Key, resource.Value);
                        }
                    }
                    else
                    {
                        _resourcesCache.TryAdd(culture.Name, new ConcurrentDictionary<string, string>(resources.ToDictionary(r => r.Key, r => r.Value)));
                    }                 
                }
            }
        }

        private static IDictionary<string, string> LoadJsonResources(string filePath)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile(filePath, optional: true, reloadOnChange: true);
            var config = builder.Build();

            return new Dictionary<string, string>(config.AsEnumerable());
        }
    }
}