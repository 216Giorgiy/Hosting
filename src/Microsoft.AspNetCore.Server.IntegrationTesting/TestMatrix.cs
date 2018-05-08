// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Xunit;

namespace Microsoft.AspNetCore.Server.IntegrationTesting
{
    public class TestMatrix
    {
        public IList<ServerType> Servers { get; set; } = new List<ServerType>();
        public IList<string> Tfms { get; set; } = new List<string>(); // Can derive from running TFM
        public IList<ApplicationType> ApplicationTypes { get; set; } = new List<ApplicationType>(); // net461/CLR is always portable, derive from TFM
        public IList<RuntimeArchitecture> Architectures { get; set; } = new List<RuntimeArchitecture>();

        // ANCM specific...
        public IList<HostingModel> HostingModels { get; set; } = new List<HostingModel>();
        public IList<AncmVersion> AncmVersions { get; set; } = new List<AncmVersion>();

        public static TestMatrix ForServers(params ServerType[] types)
        {
            return new TestMatrix()
            {
                Servers = types
            };
        }

        public TestMatrix WithTfms(params string[] tfms)
        {
            Tfms = tfms;
            return this;
        }

        public TestMatrix WithApplicationTypes(params ApplicationType[] types)
        {
            ApplicationTypes = types;
            return this;
        }

        public TestMatrix WithAllApplicationTypes()
        {
            ApplicationTypes.Add(ApplicationType.Portable);
            ApplicationTypes.Add(ApplicationType.Standalone);
            return this;
        }
        public TestMatrix WithArchitectures(params RuntimeArchitecture[] archs)
        {
            Architectures = archs;
            return this;
        }

        public TestMatrix WithAllArchitectures()
        {
            Architectures.Add(RuntimeArchitecture.x64);
            Architectures.Add(RuntimeArchitecture.x86);
            return this;
        }

        public TestMatrix WithHostingModels(params HostingModel[] models)
        {
            HostingModels = models;
            return this;
        }

        public TestMatrix WithAllHostingModels()
        {
            HostingModels.Add(HostingModel.OutOfProcess);
            HostingModels.Add(HostingModel.InProcess);
            return this;
        }

        public TestMatrix WithAncmVersions(params AncmVersion[] versions)
        {
            AncmVersions = versions;
            return this;
        }

        public TestMatrix WithAllAncmVersions()
        {
            AncmVersions.Add(AncmVersion.AspNetCoreModule);
            AncmVersions.Add(AncmVersion.AspNetCoreModuleV2);
            return this;
        }

        /// <summary>
        /// V2 + InProc
        /// </summary>
        /// <returns></returns>
        public TestMatrix WithAncmV2InProcess() => WithAncmVersions(AncmVersion.AspNetCoreModuleV2).WithHostingModels(HostingModel.InProcess);

        public TestList Build()
        {
            var data = new TestList();
            if (!Servers.Any())
            {
                // Params error, a server is required. This will cause an xunit error.
                return data;
            }

            // TFMs. If not set then use the one from the current app
            if (!Tfms.Any())
            {
                var tfmAttribute = Assembly.GetCallingAssembly().GetCustomAttribute<TargetFrameworkAttribute>();
                if (tfmAttribute != null && !string.IsNullOrEmpty(tfmAttribute.FrameworkName))
                {
                    switch (tfmAttribute.FrameworkName)
                    {
                        case ".NETFramework,Version=v4.6.1":
                            Tfms.Add(Tfm.Net461);
                            break;
                        case ".NETCoreApp,Version=v2.0":
                            Tfms.Add(Tfm.NetCoreApp20);
                            break;
                        case ".NETCoreApp,Version=v2.1":
                            Tfms.Add(Tfm.NetCoreApp21);
                            break;
                        case ".NETCoreApp,Version=v2.2":
                            Tfms.Add(Tfm.NetCoreApp22);
                            break;
                    }
                }

                if (!Tfms.Any())
                {
                    throw new ArgumentException("No TFMs was provided and one could be detected from the caller, specify a TFM.");
                }
            }

            if (!Architectures.Any())
            {
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X86:
                        Architectures.Add(RuntimeArchitecture.x86);
                        break;
                    case Architecture.X64:
                        Architectures.Add(RuntimeArchitecture.x64);
                        break;
                    default:
                        throw new ArgumentException(RuntimeInformation.OSArchitecture.ToString());
                }
            }

            if (!ApplicationTypes.Any())
            {
                ApplicationTypes.Add(ApplicationType.Portable);
            }

            foreach (var server in Servers)
            {
                if (!ServerIsSupportedOnThisOS(server))
                {
                    continue;
                }

                foreach (var tfm in Tfms)
                {
                    if (!TfmIsSupportedOnThisOS(tfm))
                    {
                        continue;
                    }

                    foreach (var t in ApplicationTypes)
                    {
                        var type = t;
                        if (Tfm.Matches(Tfm.Net461, tfm) && type == ApplicationType.Portable)
                        {
                            if (ApplicationTypes.Count == 1)
                            {
                                // Override the default
                                type = ApplicationType.Standalone;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        foreach (var arch in Architectures)
                        {
                            if (server == ServerType.IISExpress)
                            {
                                CreateIISVariations(data, server, tfm, type, arch);
                            }
                            else
                            {
                                data.Add(new TestVariant()
                                {
                                    Server = server,
                                    Tfm = tfm,
                                    ApplicationType = type,
                                    Architecture = arch,
                                });
                            }
                        }
                    }
                }
            }

            return data;
        }

        private void CreateIISVariations(TestList data, ServerType server, string tfm, ApplicationType type, RuntimeArchitecture arch)
        {
            if (!AncmVersions.Any())
            {
                AncmVersions.Add(AncmVersion.AspNetCoreModule);
            }

            if (!HostingModels.Any())
            {
                HostingModels.Add(HostingModel.OutOfProcess);
            }

            foreach (var version in AncmVersions)
            {
                foreach (var hostingModel in HostingModels)
                {
                    if (Tfm.Matches(Tfm.Net461, tfm) && hostingModel == HostingModel.InProcess)
                    {
                        continue;
                    }
                    if (version == AncmVersion.AspNetCoreModuleV2 || hostingModel == HostingModel.OutOfProcess)
                    {
                        data.Add(new TestVariant()
                        {
                            Server = server,
                            Tfm = tfm,
                            ApplicationType = type,
                            Architecture = arch,
                            AncmVersion = version,
                            HostingModel = hostingModel,
                        });
                    }
                }
            }
        }

        private static bool TfmIsSupportedOnThisOS(string tfm)
        {
            return !(Tfm.Matches(Tfm.Net461, tfm) && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        }

        private static bool ServerIsSupportedOnThisOS(ServerType server)
        {
            switch (server)
            {
                case ServerType.IIS:
                case ServerType.IISExpress:
                case ServerType.HttpSys:
                    return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                case ServerType.Kestrel:
                    return true;
                case ServerType.Nginx:
                    return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows); // Technically it's possible but we don't test it.
                default:
                    throw new ArgumentException(server.ToString());
            }
        }
    }
}
