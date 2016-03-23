// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Configuration;

namespace stress.console
{
    internal class MultiFunctionCmdArgs
    {
        public const int INVALID_ARGS = -2;

        public string Command
        {
            get
            {
                return _command ?? Assembly.GetEntryAssembly().GetName().Name;
            }
            set
            {
                _command = value;
            }
        }

        public string Description { get; set; }

        public void AddFunction<T>(string command, Func<T, int> function)
            where T : CmdArgsBase, new()
        {
            T args = new T();

            args.Command = "  " + this.Command + " " + command;

            Func<CmdArgsBase, int> genFunc = (genArgs) => { return function((T)args); };

            _cmdFunctions.Add(command, new CmdFunction() { Args = args, Function = genFunc });
        }

        public int InvokeFunctionWithArgs(string[] args)
        {
            if ((args.Length == 0) || (args[0] == "/?") || (args[0] == "-?"))
            {
                this.PrintUsage();

                return INVALID_ARGS;
            }

            if ((args[0] == "/??") || (args[0] == "-??"))
            {
                this.PrintDetailedUsage();

                return INVALID_ARGS;
            }

            CmdFunction func;

            if (!_cmdFunctions.TryGetValue(args[0], out func))
            {
                Console.WriteLine();

                Console.WriteLine("Unknown command '{0}'", args[0]);

                Console.WriteLine();

                this.PrintUsage();

                return INVALID_ARGS;
            }

            //remove the command from the args array
            string[] funcArgs = new string[args.Length - 1];

            for (int i = 1; i < args.Length; i++)
            {
                funcArgs[i - 1] = args[i];
            }

            //parse the remaining args
            func.Args.ParseArgs(funcArgs);

            if (!func.Args.IsValid)
            {
                return INVALID_ARGS;
            }

            return func.Function(func.Args);
        }

        public void PrintUsage()
        {
            Console.WriteLine("{0} USAGE:", this.Command);
            Console.WriteLine();

            StringBuilder usagebuff = new StringBuilder();
            usagebuff.Append(this.Command);
            usagebuff.Append(" [/?[?]] ");

            usagebuff.Append(string.Join(" | ", _cmdFunctions.Keys.ToArray()));

            usagebuff.Append(" <funtion args>");

            foreach (var cmdPair in _cmdFunctions)
            {
                if (!string.IsNullOrEmpty(cmdPair.Value.Args.Description))
                {
                    usagebuff.Append("\n\n");

                    usagebuff.Append("   ");

                    usagebuff.Append(cmdPair.Key);

                    usagebuff.Append(":\n      ");

                    usagebuff.Append(cmdPair.Value.Args.Description);
                }
            }

            Console.WriteLine(usagebuff);

            Console.WriteLine();
        }

        public void PrintDetailedUsage()
        {
            this.PrintUsage();

            foreach (var funcArgs in _cmdFunctions.Values.Select(c => c.Args))
            {
                Console.WriteLine();

                funcArgs.PrintUsage();
            }
        }

        private Dictionary<string, CmdFunction> _cmdFunctions = new Dictionary<string, CmdFunction>(StringComparer.OrdinalIgnoreCase);

        private string _command;

        private class CmdFunction
        {
            public Func<CmdArgsBase, int> Function { get; set; }

            public CmdArgsBase Args { get; set; }
        }
    }

    internal abstract class CmdArgsBase
    {
        protected CmdArgsBase()
        {
            this.LoadArgProps();
        }

        public string Command
        {
            get
            {
                return _command ?? Assembly.GetEntryAssembly().GetName().Name;
            }
            set
            {
                _command = value;
            }
        }

        public string Description { get; set; }

        public bool ParseArgs(string[] args)
        {
            this.LoadAppSettings();

            this.LoadArgs(args);

            this.PopulateArgValues();

            return _prereqMet;
        }

        private void LoadArgProps()
        {
            List<CmdArgAttribute> ordinalList = new List<CmdArgAttribute>();

            foreach (var prop in this.GetType().GetProperties())
            {
                var attr = Attribute.GetCustomAttribute(prop, typeof(CmdArgAttribute), true) as CmdArgAttribute;

                if (attr != null)
                {
                    //if the type specified in the CmdArgAttribute cannot be assigned to the property throw an exception
                    if (!prop.PropertyType.IsAssignableFrom(attr.Type))
                    {
                        throw new ArgumentException(string.Format("Invalid CmdArgAttribute: The arg property {0} is not assignable from type type {1}.", prop.Name, attr.Type.FullName));
                    }

                    _settingProps.Add(attr, prop);

                    //if the cmdarg is an ordinal cmdarg add it to ordinalList
                    if (attr.IsOrdinal())
                    {
                        ordinalList.Add(attr);
                    }
                }
            }

            //if there are ordinal arguments validate they are ordered properly and conform to all cmdargsbase rules
            if (ordinalList.Count > 0)
            {
                this.ValidateAndStoreOrdinalArgs(ordinalList);
            }
        }

        private void ValidateAndStoreOrdinalArgs(List<CmdArgAttribute> ordinalList)
        {
            bool hasOptionalOrdinal = false;

            //sort the list to validate that the ordinal args conform to rules
            ordinalList.Sort((a, b) => { return a.Index.CompareTo(b.Index); });

            for (int i = 0; i < ordinalList.Count; i++)
            {
                //if the index is not equal to the index of the list the ordinal arg indexes are out of order, non-contiguous, or duplicated
                //throw an exception
                if (ordinalList[i].Index != i)
                {
                    throw new ArgumentException("Invalid CmdArgAttribute: Ordinal arguments must have unique, contiguous indexes starting at 0");
                }

                //if the ordinal arg is required and there are already optional ordinal args 
                //the arg class is invalid throw an exception
                if (ordinalList[i].Required && hasOptionalOrdinal)
                {
                    throw new ArgumentException("Invalid CmdArgAttribute: Required ordinal arguments cannot be specified after optional ordinal arguments");
                }
            }

            //if there are optional ordinal args keyed args are not allowed
            //if there are keyed args throw an exception
            //detect keyed args by the difference in length between ordinalList and this.settingsProps
            if (hasOptionalOrdinal && (_settingProps.Count != ordinalList.Count))
            {
                throw new ArgumentException("Invalid CmdArgAttribute: Keyed args are not permitted when using optional ordinal arguments");
            }

            //store the ordinal args
            _ordinalArgs = ordinalList.ToArray();
        }

        public void PrintSettings()
        {
            Console.WriteLine("Settings:");

            foreach (var entry in _settings)
            {
                Console.WriteLine("\t{0}:\t{1}", entry.Key, entry.Value);
            }
        }

        public bool IsValid
        {
            get
            {
                return _prereqMet;
            }
        }

        public virtual void PrintUsage()
        {
            Console.WriteLine("{0} USAGE:", this.Command);
            Console.WriteLine();

            StringBuilder usagebuff = new StringBuilder();
            usagebuff.Append(this.Command);
            usagebuff.Append(" [/?] ");


            //print usage of the ordinal args
            for (int i = 0; i < _ordinalArgs.Length; i++)
            {
                if (!_ordinalArgs[i].Required)
                {
                    usagebuff.Append("[");
                }
                else
                {
                    usagebuff.Append("<");
                }

                //if no value moniker was specifed use %index instead
                usagebuff.Append(_ordinalArgs[i].ValueMoniker ?? "%" + i.ToString());

                if (!_ordinalArgs[i].Required)
                {
                    usagebuff.Append("]");
                }
                else
                {
                    usagebuff.Append(">");
                }

                usagebuff.Append(" ");
            }

            //print usage of the keyed args
            foreach (CmdArgAttribute attr in _settingProps.Keys.OrderByDescending(a => a.Required))
            {
                if (!attr.IsOrdinal())
                {
                    if (!attr.Required)
                    {
                        usagebuff.Append("[");
                    }

                    usagebuff.Append("/");

                    usagebuff.Append(attr.Key);

                    if (attr.Type != typeof(bool))
                    {
                        usagebuff.Append(":");

                        usagebuff.Append(attr.ValueMoniker ?? "value");
                    }

                    if (!attr.Required)
                    {
                        usagebuff.Append("]");
                    }

                    usagebuff.Append(" ");
                }
            }

            Console.WriteLine(usagebuff);

            usagebuff.Clear();

            //print the details of the ordinal arguments
            foreach (var attr in _ordinalArgs)
            {
                this.AppendArgDetailsToBuff(attr, usagebuff);
            }


            //print the keyed arg details
            foreach (CmdArgAttribute attr in _settingProps.Keys.OrderByDescending(a => a.Required))
            {
                //only print details of the keyed args as we printed the ordinal arg details above
                if (attr.IsKeyed())
                {
                    this.AppendArgDetailsToBuff(attr, usagebuff);
                }
            }

            Console.WriteLine(usagebuff);

            Console.WriteLine();
        }

        private void AppendArgDetailsToBuff(CmdArgAttribute attr, StringBuilder usagebuff)
        {
            if (!string.IsNullOrEmpty(attr.Description))
            {
                usagebuff.Append("\n\n");

                usagebuff.Append("   ");
                if (!attr.IsOrdinal())
                {
                    usagebuff.Append("/");
                }

                //if the arg is keyed print the key before the description
                //otherwise if the arg is ordinal print the valuemoniker if it isn't null
                //if the value moniker is null print %index 
                usagebuff.Append(attr.IsKeyed() ? attr.Key : attr.ValueMoniker ?? "%" + attr.Index.ToString());

                //if there are alternate keys append them
                if (attr.AlternateKeys != null && attr.AlternateKeys.Length > 0)
                {
                    usagebuff.Append(" (/");

                    usagebuff.Append(string.Join(" | /", attr.AlternateKeys));

                    usagebuff.Append(")");
                }

                usagebuff.Append(":\n      ");

                usagebuff.Append(attr.Description);
            }
        }


        protected virtual bool PopulateArgValues()
        {
            foreach (var entry in _settingProps)
            {
                object val;

                //if we have a setting value matching the key of matching the settings property 
                if (this.FindArgValue(entry.Key, out val))
                {
                    entry.Value.SetValue(this, val);
                }
                else if (entry.Key.Default != null)
                {
                    entry.Value.SetValue(this, entry.Key.Default);
                }
                else
                {
                    _prereqMet &= !entry.Key.Required;
                }
            }

            return _prereqMet;
        }

        protected bool FindArgValue(CmdArgAttribute argAttr, out object val)
        {
            bool found = false;

            val = null;

            if (found = _settings.ContainsKey(argAttr.Key))
            {
                val = this.GetSettingValue(argAttr.Key, argAttr.Type);
            }

            if (!found && (argAttr.AlternateKeys != null))
            {
                for (int i = 0; i < argAttr.AlternateKeys.Length && !found; i++)
                {
                    if (found = _settings.ContainsKey(argAttr.AlternateKeys[i]))
                    {
                        val = this.GetSettingValue(argAttr.AlternateKeys[i], argAttr.Type);
                    }
                }
            }

            return found;
        }

        protected object GetSettingValue(string key, Type type)
        {
            object ret = null;

            if (type == typeof(bool) || type == typeof(bool?))
            {
                ret = GetBoolValue(key);
            }
            else if (type == typeof(int) || type == typeof(int?))
            {
                ret = GetIntValue(key);
            }
            else if (type == typeof(TimeSpan) || type == typeof(TimeSpan?))
            {
                ret = GetTimeSpanValue(key);
            }
            else if (type == typeof(string[]))
            {
                ret = GetStringArrayValue(key);
            }
            else if (type == typeof(string))
            {
                ret = GetValue(key);
            }

            return ret;
        }

        private bool GetBoolValue(string key)
        {
            bool val = false;

            string s = this.GetValue(key);

            //if no value was specified the presents of the arg key specifies the parameter is set to true 
            //i.e. you have consoleapp.exe [/diag[:true | false]] the user can specify /diag as a shortcut to /diag:true
            if (s == null)
            {
                val = _settings.ContainsKey(key);
            }
            else
            {
                bool.TryParse(s, out val);
            }

            return val;
        }

        private int GetIntValue(string key)
        {
            int val;

            string s = this.GetValue(key);

            int.TryParse(s, out val);

            return val;
        }

        private TimeSpan GetTimeSpanValue(string key)
        {
            TimeSpan val;

            string s = this.GetValue(key);

            TimeSpan.TryParse(s, out val);

            return val;
        }

        private string[] GetStringArrayValue(string key)
        {
            string[] val = null;

            string s = this.GetValue(key);

            if (s != null)
            {
                val = s.Split(';');
            }

            return val;
        }

        private string GetValue(string key)
        {
            string val;

            _settings.TryGetValue(key, out val);

            return val;
        }

        private void LoadArgs(string[] args)
        {
            //if the help was specified print the usage and return 
            if (args[0] == "/?" || args[0] == "-?")
            {
                this.PrintUsage();

                _prereqMet = false;

                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string s = args[i];
                string key, val;

                //if all the oridinal args don't have value yet, add the full value as a ordinal arg
                if (i < _ordinalArgs.Length)
                {
                    key = _ordinalArgs[i].Key;

                    val = args[i];

                    _settings[key] = val;
                }
                //otherwise treat as a keyed arg should start with '/' or '-'
                else if (s.StartsWith("/") || s.StartsWith("-"))
                {
                    int splitIndex = s.IndexOf(':');

                    //if the ':' is not present in the string add the key to the dictionary with a value of null
                    if (splitIndex < 0)
                    {
                        key = s.Substring(1, s.Length - 1);
                        val = null;
                    }
                    else
                    {
                        key = s.Substring(1, splitIndex - 1);

                        //if ':' is the last character in the arg add the key to the dictionary with a value of string.Empty 
                        val = (splitIndex == (s.Length - 1)) ? string.Empty : s.Substring(splitIndex + 1, s.Length - splitIndex - 1);
                    }

                    _settings[key] = val;
                }
                //if there is an argument after the ordinal arguments which doesn't have a 
                //proper key ie (-<key>[:val] or /<key>[:val] then the validation of the args 
                //should fail set prereqMet to false
                else
                {
                    _prereqMet = false;
                }
            }

            foreach (string s in args)
            {
                if (s.StartsWith("/"))
                {
                    int splitIndex = s.IndexOf(':');

                    string key, val;

                    //if the ':' is not present in the string add the key to the dictionary with a value of null
                    if (splitIndex < 0)
                    {
                        key = s.Substring(1, s.Length - 1);
                        val = null;
                    }
                    else
                    {
                        key = s.Substring(1, splitIndex - 1);

                        //if ':' is the last character in the arg add the key to the dictionary with a value of string.Empty 
                        val = (splitIndex == (s.Length - 1)) ? string.Empty : s.Substring(splitIndex + 1, s.Length - splitIndex - 1);
                    }

                    _settings[key] = val;
                }
            }
        }

        private void LoadAppSettings()
        {
            foreach (var key in ConfigurationManager.AppSettings.AllKeys)
            {
                _settings[key] = ConfigurationManager.AppSettings[key];
            }
        }

        private bool _prereqMet = true;

        private string _command = null;

        private Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<CmdArgAttribute, PropertyInfo> _settingProps = new Dictionary<CmdArgAttribute, PropertyInfo>();
        private CmdArgAttribute[] _ordinalArgs = new CmdArgAttribute[0];

        private int _ordinalCount = 0;
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    internal class CmdArgAttribute : Attribute
    {
        public CmdArgAttribute(Type type, int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException("index");

            if (type == null) throw new ArgumentNullException("type");

            this.Index = index;

            this.Key = "%" + index.ToString();

            this.Type = type;
        }

        public CmdArgAttribute(Type type, string key, params string[] alternateKeys)
        {
            if (key == null) throw new ArgumentNullException("key");

            if (type == null) throw new ArgumentNullException("type");

            this.Key = key;

            this.Type = type;

            this.AlternateKeys = alternateKeys;

            this.Index = -1;
        }

        public int Index
        {
            get;
            private set;
        }

        public string Key
        {
            get;
            private set;
        }

        public string[] AlternateKeys
        {
            get;
            private set;
        }

        public Type Type
        {
            get;
            private set;
        }

        public bool Required
        {
            get;
            set;
        }

        public object Default
        {
            get;
            set;
        }

        public string ValueMoniker
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public bool IsOrdinal()
        {
            return this.Index >= 0;
        }

        public bool IsKeyed()
        {
            return this.Index < 0;
        }
    }
}
