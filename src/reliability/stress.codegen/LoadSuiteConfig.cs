// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using stress.execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class LoadSuiteConfig
    {
        public LoadSuiteConfig()
        {
            this.LoadTestConfigs = new List<LoadTestConfig>();
        }

        public List<LoadTestConfig> LoadTestConfigs;

        public string Host;

        public void Serialize(string path)
        {
            // Serialize the RunConfiguration
            JsonSerializer serializer = JsonSerializer.CreateDefault();

            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    serializer.Serialize(writer, this);
                }
            }
        }

        public static LoadSuiteConfig Deserialize(string path)
        {
            LoadSuiteConfig config = null;

            // Deserialize the RunConfiguration
            JsonSerializer serializer = JsonSerializer.CreateDefault();

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(fs))
                {
                    JsonTextReader jReader = new JsonTextReader(reader);

                    // Call the Deserialize method to restore the object's state.
                    config = serializer.Deserialize<LoadSuiteConfig>(jReader);
                }
            }

            return config;
        }
    }
}
