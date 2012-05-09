using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Cassette.IO;

namespace Cassette.Caching
{
    class BundleCollectionCacheReader
    {
        readonly IDirectory cacheDirectory;
        readonly string manifestFilename;
        readonly Func<string, IBundleDeserializer<Bundle>> getDeserializerForBundleTypeName;

        public BundleCollectionCacheReader(IDirectory cacheDirectory, string manifestFilename, Func<string, IBundleDeserializer<Bundle>> getDeserializerForBundleTypeName)
        {
            this.cacheDirectory = cacheDirectory;
            this.manifestFilename = manifestFilename;
            this.getDeserializerForBundleTypeName = getDeserializerForBundleTypeName;
        }
        
        public CacheReadResult Read()
        {
            var file = cacheDirectory.GetFile(manifestFilename);
            if (file.Exists)
            {
                try
                {
                    // TODO: read IsPrecompiled
                    return CacheReadResult.Success(CreateManifest(file));
                }
                catch
                {
                    return CacheReadResult.Failed();
                }
            }
            else
            {
                return CacheReadResult.Failed();
            }
        }

        Manifest CreateManifest(IFile manifestFile)
        {
            var manifestElement = ManifestElement(manifestFile);
            var bundles = DeserializeBundles(manifestFile);
            var version = manifestElement.Attribute("Version").Value;
            var isPrecompiled = bool.Parse(manifestElement.Attribute("IsPrecompiled").Value);
            return new Manifest(
                bundles,
                version,
                manifestFile.LastWriteTimeUtc,
                isPrecompiled
            );
        }

        XElement ManifestElement(IFile manifestFile)
        {
            using (var manifestStream = manifestFile.OpenRead())
            {
                var manifestDocument = XDocument.Load(manifestStream);
                return manifestDocument.Root;
            }
        }

        IEnumerable<Bundle> DeserializeBundles(IFile manifestFile)
        {
            using (var manifestStream = manifestFile.OpenRead())
            {
                var manifestDocument = XDocument.Load(manifestStream);
                var bundleElements = manifestDocument.Root.Elements();
                return CreateBundles(bundleElements).ToArray();
            }
        }

        IEnumerable<Bundle> CreateBundles(IEnumerable<XElement> bundleElements)
        {
            return from bundleElement in bundleElements
                   let deserializer = GetDeserializer(bundleElement)
                   select deserializer.Deserialize(bundleElement, cacheDirectory);
        }

        IBundleDeserializer<Bundle> GetDeserializer(XElement bundleElement)
        {
            var bundleTypeName = bundleElement.Name.LocalName;
            return getDeserializerForBundleTypeName(bundleTypeName);
        }
    }
}