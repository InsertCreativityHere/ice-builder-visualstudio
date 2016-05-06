// **********************************************************************
//
// Copyright (c) 2009-2016 ZeroC, Inc. All rights reserved.
//
// **********************************************************************

#region using
using System;
using System.Collections.Generic;
using System.IO;

using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#endregion

namespace IceBuilder
{
    public class TaskUtil
    {
        public static String MakeRelative(String from, String to)
        {
            if (!Path.IsPathRooted(from))
            {
                throw new ArgumentException(String.Format("from: `{0}' must be an absolute path", from));
            }
            else if (!Path.IsPathRooted(to))
            {
                return to;
            }

            string[] firstPathParts = Path.GetFullPath(from).Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
            string[] secondPathParts = Path.GetFullPath(to).Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);

            int sameCounter = 0;
            while (sameCounter < Math.Min(firstPathParts.Length, secondPathParts.Length) &&
                String.Equals(firstPathParts[sameCounter], secondPathParts[sameCounter],
                StringComparison.CurrentCultureIgnoreCase))
            {
                ++sameCounter;
            }

            // Different volumes, relative path not possible.
            if (sameCounter == 0)
            {
                return to;
            }

            // Pop back up to the common point.
            String newPath = "";
            for (int i = sameCounter; i < firstPathParts.Length; ++i)
            {
                newPath += ".." + Path.DirectorySeparatorChar;
            }
            // Descend to the target.
            for (int i = sameCounter; i < secondPathParts.Length; ++i)
            {
                newPath += secondPathParts[i] + Path.DirectorySeparatorChar;
            }
            return newPath.TrimEnd(Path.DirectorySeparatorChar);
        }
    }
    #region SliceCompilerTask
    public abstract class SliceCompilerTask : ToolTask
    {
        [Required]
        public String WorkingDirectory
        {
            get;
            set;
        }

        [Required]
        public String IceHome
        {
            get;
            set;
        }

        [Required]
        public String IceToolsPath
        {
            get;
            set;
        }

        [Required]
        public String OutputDir
        {
            get;
            set;
        }

        [Required]
        public ITaskItem[] Sources
        {
            get;
            set;
        }

        [Required]
        public String DependFile
        {
            get;
            set;
        }

        public Boolean Depend
        {
            get;
            set;
        }

        public Boolean AllowIcePrefix
        {
            get;
            set;
        }

        public Boolean Underscore
        {
            get;
            set;
        }

        public Boolean Stream
        {
            get;
            set;
        }

        public Boolean Checksum
        {
            get;
            set;
        }

        public String[] IncludeDirectories
        {
            get;
            set;
        }

        public String AdditionalOptions
        {
            get;
            set;
        }

        protected override String GetWorkingDirectory()
        {
            return WorkingDirectory;
        }

        protected virtual String GetGeneratedPath(ITaskItem item, String outputDir, String ext)
        {
            return Path.Combine(
                outputDir,
                Path.GetFileName(Path.ChangeExtension(item.GetMetadata("Identity"), ext)));
        }

        protected abstract String GeneratedExtensions
        {
            get;
        }

        protected abstract void TraceGenerated();

        protected override String GenerateCommandLineCommands()
        {
            UsageError = false;
            CommandLineBuilder builder = new CommandLineBuilder(false);
            if (Depend)
            {
                builder.AppendSwitch("--depend-xml");
                builder.AppendSwitch("--depend-file");
                builder.AppendFileNameIfNotNull(DependFile);
            }

            if (!String.IsNullOrEmpty(OutputDir))
            {
                builder.AppendSwitch("--output-dir");
                builder.AppendFileNameIfNotNull(OutputDir);
            }

            if (AllowIcePrefix)
            {
                builder.AppendSwitch("--ice");
            }

            if (Underscore)
            {
                builder.AppendSwitch("--underscore");
            }

            if (Stream)
            {
                builder.AppendSwitch("--stream");
            }

            if (Checksum)
            {
                builder.AppendSwitch("--checksum");
            }

            if (IncludeDirectories != null)
            {
                foreach (String path in IncludeDirectories)
                {
                    builder.AppendSwitchIfNotNull("-I", path);
                }
            }

            if (!String.IsNullOrEmpty(AdditionalOptions))
            {
                builder.AppendTextUnquoted(" ");
                builder.AppendTextUnquoted(AdditionalOptions);
            }

            builder.AppendFileNamesIfNotNull(Sources, " ");

            return builder.ToString();
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            try
            {
                if (!Depend)
                {
                    TraceGenerated();
                }
                return base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
            }
            catch(Exception ex)
            {
                Log.LogError(ex.ToString());
                throw;
            }
        }

        protected override string GenerateFullPathToTool()
        {
            String home = IceHome;
            String path = Path.Combine(IceToolsPath, ToolName);
            if (!File.Exists(path))
            {
                const String message =
                    "Slice compiler `{0}' not found. Review Ice Home setting in Visual Studio 'Tool > Options > Ice'";
                Log.LogError(String.Format(message, path));
            }
            return path;
        }

        protected override void LogToolCommand(string message)
        {
            Log.LogMessage(MessageImportance.Low, message);
        }

        private bool UsageError
        {
            get;
            set;
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            if (UsageError)
            {
                return;
            }

            int i = singleLine.IndexOf(String.Format("{0}:", ToolName));
            if (i != -1)
            {
                i += (ToolName.Length + 1);
                Log.LogError("", "", "", "", 0, 0, 0, 0,
                    String.Format("{0}: {1}", Path.GetFileName(ToolName), singleLine.Substring(i)));
                UsageError = true;
            }
            else
            {
                String s = singleLine.Trim();
                if (s.StartsWith(WorkingDirectory))
                {
                    s = s.Substring(WorkingDirectory.Length);
                }

                String file = "";
                int line = 0;
                String description = "";

                //
                // Skip the drive letter
                //
                i = s.IndexOf(":");
                if (i <= 1 && s.Length > i + 1)
                {
                    i = s.IndexOf(":", i + 1);
                }

                if (i != -1)
                {
                    file = Path.GetFullPath(s.Substring(0, i).Trim().Trim('"'));
                    if (file.IndexOf(WorkingDirectory) != -1)
                    {
                        file = file.Substring(WorkingDirectory.Length)
                                   .Trim(Path.DirectorySeparatorChar);
                    }

                    if (s.Length > i + 1)
                    {
                        s = s.Substring(i + 1);

                        i = s.IndexOf(":");
                        if (i != -1)
                        {
                            if (Int32.TryParse(s.Substring(0, i), out line))
                            {
                                if (s.Length > i + 1)
                                {
                                    s = s.Substring(i + 1);
                                }
                            }
                            else
                            {
                                s = s.Substring(i);
                            }
                        }


                        description = s.Trim();
                        description += Environment.NewLine;
                    }
                }

                if (description.IndexOf("warning:") == 0)
                {
                    //
                    // Don't emit warnings while parsing dependencies otherwise
                    // they will appear twices in the Error List and Output.
                    //
                    if (!Depend)
                    {
                        Log.LogWarning("", "", "", file, line - 1, 0, 0, 0, description.Substring("warning:".Length));
                    }
                }
                else if (description.IndexOf("error:") == 0)
                {
                    Log.LogError("", "", "", file, line - 1, 0, 0, 0, description.Substring("error:".Length));
                }
                else if (!String.IsNullOrEmpty(description))
                {
                    Log.LogError("", "", "", file, line - 1, 0, 0, 0, description);
                }
            }
        }
    }
    #endregion

    #region Slice2CppTask
    public class Slice2CppTask : SliceCompilerTask
    {
        public Slice2CppTask()
        {
            HeaderExt = "h";
            SourceExt = "cpp";
        }

        protected override string ToolName
        {
            get
            {
                return "slice2cpp.exe";
            }
        }

        public String HeaderOutputDir
        {
            get;
            set;
        }

        public String DLLExport
        {
            get;
            set;
        }

        public String HeaderExt
        {
            get;
            set;
        }

        public String SourceExt
        {
            get;
            set;
        }

        public String BaseDirectoryForGeneratedInclude
        {
            get;
            set;
        }

        protected override String GeneratedExtensions
        {
            get
            {
                return String.Format("{0},{1}", HeaderExt, SourceExt);
            }
        }

        protected override String GenerateCommandLineCommands()
        {
            CommandLineBuilder builder = new CommandLineBuilder(false);

            if (!String.IsNullOrEmpty(DLLExport))
            {
                builder.AppendSwitch("--dll-export");
                builder.AppendFileNameIfNotNull(DLLExport);
            }

            if (!HeaderExt.Equals("h"))
            {
                builder.AppendSwitch("--header-ext");
                builder.AppendFileNameIfNotNull(HeaderExt);
            }

            if (!SourceExt.Equals("cpp"))
            {
                builder.AppendSwitch("--source-ext");
                builder.AppendFileNameIfNotNull(SourceExt);
            }

            if (!String.IsNullOrEmpty(BaseDirectoryForGeneratedInclude))
            {
                builder.AppendSwitch("--include-dir");
                builder.AppendFileNameIfNotNull(BaseDirectoryForGeneratedInclude);
            }
            builder.AppendTextUnquoted(" ");
            builder.AppendTextUnquoted(base.GenerateCommandLineCommands());

            return builder.ToString();
        }

        protected override void TraceGenerated()
        {
            foreach (ITaskItem source in Sources)
            {
                String message = String.Format("Compiling {0} Generating -> ", source.GetMetadata("Identity"));
                message += TaskUtil.MakeRelative(WorkingDirectory, GetGeneratedPath(source, OutputDir, SourceExt));
                message += " and ";
                message += TaskUtil.MakeRelative(WorkingDirectory,
                    GetGeneratedPath(source, String.IsNullOrEmpty(HeaderOutputDir) ? OutputDir : HeaderOutputDir, HeaderExt));
                Log.LogMessage(MessageImportance.High, message);
            }
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            try
            {
                int status = base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
                if (!String.IsNullOrEmpty(HeaderOutputDir) && !Depend && status == 0)
                {
                    if (!Directory.Exists(HeaderOutputDir))
                    {
                        Directory.CreateDirectory(HeaderOutputDir);
                    }
                    foreach (ITaskItem source in Sources)
                    {
                        String sourceH = GetGeneratedPath(source, OutputDir, HeaderExt);
                        String targetH = GetGeneratedPath(source, HeaderOutputDir, HeaderExt);
                        if (!File.Exists(targetH) || new FileInfo(targetH).LastWriteTime < new FileInfo(sourceH).LastWriteTime)
                        {
                            if (File.Exists(targetH))
                            {
                                File.Delete(targetH);
                            }
                            File.Move(sourceH, targetH);
                        }

                        if (File.Exists(sourceH))
                        {
                            File.Delete(sourceH);
                        }
                    }
                }
                return status;
            }
            catch(Exception ex)
            {
                Log.LogError(ex.ToString());
                throw;
            }
        }
    }
    #endregion

    #region Slice2CSharpTask
    public class Slice2CSharpTask : SliceCompilerTask
    {
        protected override String ToolName
        {
            get
            {
                return "slice2cs.exe";
            }
        }

        public Boolean Tie
        {
            get;
            set;
        }

        protected override String GeneratedExtensions
        {
            get
            {
                return "cs";
            }
        }

        protected override void TraceGenerated()
        {
            foreach (ITaskItem source in Sources)
            {
                String message = String.Format("Compiling {0} Generating -> ", source.GetMetadata("Identity"));
                message += TaskUtil.MakeRelative(WorkingDirectory, GetGeneratedPath(source, OutputDir, ".cs"));
                Log.LogMessage(MessageImportance.High, message);
            }
        }

        protected override String GenerateCommandLineCommands()
        {
            CommandLineBuilder builder = new CommandLineBuilder();
            if (Tie)
            {
                builder.AppendSwitch("--tie ");
            }
            builder.AppendTextUnquoted(String.Format(" {0}", base.GenerateCommandLineCommands()));
            return builder.ToString();
        }
    }
    #endregion

    #region Slice2PhpTask
    public class Slice2PhpTask : SliceCompilerTask
    {
        protected override String ToolName
        {
            get
            {
                return "slice2php.exe";
            }
        }

        public Boolean All
        {
            get;
            set;
        }

        public Boolean Namespace
        {
            get;
            set;
        }

        protected override String GeneratedExtensions
        {
            get
            {
                return "php";
            }
        }

        protected override void TraceGenerated()
        {
            foreach (ITaskItem source in Sources)
            {
                String message = String.Format("Compiling {0} Generating -> ", source.GetMetadata("Identity"));
                message += TaskUtil.MakeRelative(WorkingDirectory, GetGeneratedPath(source, OutputDir, ".php"));
                Log.LogMessage(MessageImportance.High, message);
            }
        }

        protected override String GenerateCommandLineCommands()
        {
            CommandLineBuilder builder = new CommandLineBuilder();
            if (All)
            {
                builder.AppendSwitch("--all ");
            }
            if (Namespace)
            {
                builder.AppendSwitch("--namespace ");
            }
            builder.AppendTextUnquoted(String.Format(" {0}", base.GenerateCommandLineCommands()));
            return builder.ToString();
        }
    }
    #endregion

    #region SliceDependTask
    public abstract class SliceDependTask : Task
    {
        [Required]
        public ITaskItem[] Sources
        {
            get;
            set;
        }

        [Required]
        public String OutputDir
        {
            get;
            set;
        }

        [Required]
        public String SliceCompiler
        {
            get;
            set;
        }

        [Required]
        public String WorkingDirectory
        {
            get;
            set;
        }

        [Required]
        public String DependFile
        {
            get;
            set;
        }

        [Required]
        public String CommandLog
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] ComputedSources
        {
            get;
            private set;
        }

        [Output]
        public bool UpdateDepends
        {
            get;
            private set;
        }

        abstract protected ITaskItem[] GeneratedItems(ITaskItem source);

        protected virtual String GetGeneratedPath(ITaskItem item, String outputDir, String ext)
        {
            return Path.Combine(
                outputDir,
                Path.GetFileName(Path.ChangeExtension(item.GetMetadata("Identity"), ext)));
        }

        public override bool Execute()
        {
            try
            {
                List<ITaskItem> computed = new List<ITaskItem>();
                UpdateDepends = false;

                //
                // Compare the command log files to detect changes in build options.
                //
                String log0 = String.Format(CommandLog);
                String log1 = String.Format(Path.ChangeExtension(CommandLog, ".0.log"));
                bool logChanged = false;
                if(!File.Exists(log1))
                {
                    logChanged = true;
                    Log.LogMessage(MessageImportance.Low,
                        String.Format("Build required because command log file: {0} doesn't exists",
                                      TaskUtil.MakeRelative(WorkingDirectory, log1)));
                }
                else if(!FileCompare(log0, log1))
                {
                    logChanged = true;
                    Log.LogMessage(
                        MessageImportance.Low, "Build required because builder options changed");
                }

                if(File.Exists(log1))
                {
                    File.Delete(log1);
                }
                File.Move(log0, log1);

                FileInfo sliceCompiler = new FileInfo(SliceCompiler);

                XmlDocument dependsDoc = new XmlDocument();
                bool dependExists = File.Exists(DependFile);

                //
                // If command log file changed we don't need to compute dependencies as all
                // files must be rebuild
                //
                if(!logChanged)
                {
                    if (dependExists)
                    {
                        try
                        {
                            dependsDoc.Load(DependFile);
                        }
                        catch (XmlException)
                        {
                            try
                            {
                                File.Delete(DependFile);
                            }
                            catch (IOException)
                            {
                            }
                            Log.LogMessage(MessageImportance.Low,
                                String.Format("Build required because depend file: {0} has some invalid data",
                                    TaskUtil.MakeRelative(WorkingDirectory, DependFile)));
                        }
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low,
                            String.Format("Build required because depend file: {0} doesn't exists",
                                          TaskUtil.MakeRelative(WorkingDirectory, DependFile)));
                    }
                }

                foreach (ITaskItem source in Sources)
                {
                    bool skip = !logChanged && dependExists;
                    Log.LogMessage(MessageImportance.Low,
                        String.Format("Computing dependencies for {0}", source.GetMetadata("Identity")));

                    ITaskItem[] generatedItems = GeneratedItems(source);

                    FileInfo sourceInfo = new FileInfo(source.GetMetadata("FullPath"));
                    if (!sourceInfo.Exists)
                    {
                        Log.LogMessage(MessageImportance.Low,
                            String.Format("Build required because source: {0} doesn't exists",
                                source.GetMetadata("Identity")));
                        skip = false;
                    }

                    FileInfo generatedInfo = null;
                    //
                    // Check if the Slice compiler is older than the source file
                    //
                    if(skip)
                    {
                        foreach (ITaskItem item in generatedItems)
                        {
                            generatedInfo = new FileInfo(item.GetMetadata("FullPath"));

                            if (generatedInfo.Exists &&
                                sliceCompiler.LastWriteTime.ToFileTime() > generatedInfo.LastWriteTime.ToFileTime())
                            {
                                Log.LogMessage(MessageImportance.Low,
                                        String.Format("Build required because target: {0} is older than Slice compiler: {1}",
                                            TaskUtil.MakeRelative(WorkingDirectory, generatedInfo.FullName),
                                            Path.GetFileName(SliceCompiler)));
                                skip = false;
                                break;
                            }
                        }
                    }

                    if (skip)
                    {
                        foreach (ITaskItem item in generatedItems)
                        {
                            generatedInfo = new FileInfo(item.GetMetadata("FullPath"));
                            if (!generatedInfo.Exists)
                            {
                                Log.LogMessage(MessageImportance.Low,
                                    String.Format("Build required because generated: {0} doesn't exists",
                                        TaskUtil.MakeRelative(WorkingDirectory, generatedInfo.FullName)));
                                skip = false;
                                break;
                            }
                            else if (sourceInfo.LastWriteTime.ToFileTime() > generatedInfo.LastWriteTime.ToFileTime())
                            {
                                Log.LogMessage(MessageImportance.Low,
                                    String.Format("Build required because source: {0} is older than target {1}",
                                        source.GetMetadata("Identity"),
                                        TaskUtil.MakeRelative(WorkingDirectory, generatedInfo.FullName)));
                                skip = false;
                                break;
                            }
                        }
                    }

                    if (skip)
                    {
                        XmlNodeList depends = dependsDoc.DocumentElement.SelectNodes(
                                    String.Format("/dependencies/source[@name='{0}']/dependsOn", source.GetMetadata("Identity")));
                        if (depends != null)
                        {
                            List<String> dependPaths = new List<String>();
                            foreach (XmlNode depend in depends)
                            {
                                String path = depend.Attributes["name"].Value;
                                FileInfo dependencyInfo = new FileInfo(path);
                                if (!dependencyInfo.Exists)
                                {
                                    skip = false;
                                    Log.LogMessage(MessageImportance.Low,
                                    String.Format("Build required because dependency: {0} doesn't exists",
                                        TaskUtil.MakeRelative(WorkingDirectory, dependencyInfo.FullName)));
                                    break;
                                }
                                else if (dependencyInfo.LastWriteTime > generatedInfo.LastWriteTime)
                                {
                                    skip = false;
                                    Log.LogMessage(MessageImportance.Low,
                                    String.Format("Build required because source: {0} is older than target: {1}",
                                        source.GetMetadata("Identity"),
                                        TaskUtil.MakeRelative(WorkingDirectory, dependencyInfo.FullName)));
                                    break;
                                }
                            }
                        }
                    }

                    if (skip)
                    {
                        String message = String.Format("Skipping {0} -> ", source.GetMetadata("Identity"));
                        message += generatedItems[0].GetMetadata("Identity");
                        if (generatedItems.Length > 1)
                        {
                            message += " and ";
                            message += generatedItems[1].GetMetadata("Identity");
                            message += " are ";
                        }
                        else
                        {
                            message += " is ";
                        }
                        message += "up to date";

                        Log.LogMessage(MessageImportance.Normal, message);
                    }

                    ITaskItem computedSource = new TaskItem(source.ItemSpec);
                    source.CopyMetadataTo(computedSource);
                    computedSource.SetMetadata("BuildRequired", skip ? "False" : "True");
                    computed.Add(computedSource);

                    UpdateDepends = UpdateDepends || !skip;
                }
                ComputedSources = computed.ToArray();
                return true;
            }
            catch(Exception ex)
            {
                Log.LogError(ex.ToString());
                throw;
            }
        }

        private bool FileCompare(String file1, String file2)
        {
            FileStream fs1 = new FileStream(file1, FileMode.Open);
            FileStream fs2 = new FileStream(file2, FileMode.Open);
            int file1byte;
            int file2byte;
            try
            {
                // Check the file sizes. If they are not the same, the files
                // are not the same.
                if (fs1.Length != fs2.Length)
                {
                    // Close the file
                    fs1.Close();
                    fs2.Close();

                    // Return false to indicate files are different
                    return false;
                }

                // Read and compare a byte from each file until either a
                // non-matching set of bytes is found or until the end of
                // file1 is reached.
                do
                {
                    // Read one byte from each file.
                    file1byte = fs1.ReadByte();
                    file2byte = fs2.ReadByte();
                }
                while ((file1byte == file2byte) && (file1byte != -1));


            }
            finally
            {
                fs1.Close();
                fs2.Close();
            }

            // Return the success of the comparison. "file1byte" is
            // equal to "file2byte" at this point only if the files are
            // the same.
            return ((file1byte - file2byte) == 0);
        }
    }
    #endregion

    #region Slice2CppDependTask
    public class Slice2CppDependTask : SliceDependTask
    {
        [Required]
        public String SourceExt
        {
            get;
            set;
        }

        [Required]
        public String HeaderExt
        {
            get;
            set;
        }

        public String HeaderOutputDir
        {
            get;
            set;
        }

        protected override ITaskItem[] GeneratedItems(ITaskItem source)
        {
            return new ITaskItem[]
                {
                    new TaskItem(GetGeneratedPath(source, OutputDir, SourceExt)),
                    new TaskItem(GetGeneratedPath(source, String.IsNullOrEmpty(HeaderOutputDir) ? OutputDir : HeaderOutputDir, HeaderExt)),
                };
        }
    }
    #endregion

    #region Slice2CSharpDependTask
    public class Slice2CSharpDependTask : SliceDependTask
    {
        protected override ITaskItem[] GeneratedItems(ITaskItem source)
        {
            return new ITaskItem[]
                {
                    new TaskItem(GetGeneratedPath(source, OutputDir, ".cs")),
                };
        }

    }
    #endregion

    #region Slice2PhpDependTask
    public class Slice2PhpDependTask : SliceDependTask
    {
        protected override ITaskItem[] GeneratedItems(ITaskItem source)
        {
            return new ITaskItem[]
                {
                    new TaskItem(GetGeneratedPath(source, OutputDir, ".php")),
                };
        }

    }
    #endregion

    #region Slice2PythonTask
    public class Slice2PythonTask : SliceCompilerTask
    {
        protected override String ToolName
        {
            get
            {
                return "slice2py.exe";
            }
        }

        [Required]
        public String PythonHome
        {
            get;
            set;
        }

        public String Prefix
        {
            get;
            set;
        }

        public Boolean NoPackage
        {
            get;
            set;
        }

        protected override String GeneratedExtensions
        {
            get
            {
                return "py";
            }
        }

        protected override String GetGeneratedPath(ITaskItem item, String outputDir, String ext)
        {
            String generatedFileName = String.Format("{0}_ice.py ", item.GetMetadata("Filename"));
            if(!String.IsNullOrEmpty(Prefix))
            {
                generatedFileName = Prefix + generatedFileName;
            }
            return Path.Combine(outputDir, generatedFileName);
        }

        protected override void TraceGenerated()
        {
            foreach (ITaskItem source in Sources)
            {
                String message = String.Format("Compiling {0} Generating -> ", source.GetMetadata("Identity"));
                message += TaskUtil.MakeRelative(WorkingDirectory, GetGeneratedPath(source, OutputDir, GeneratedExtensions));
                Log.LogMessage(MessageImportance.High, message);
            }
        }

        protected override String GenerateCommandLineCommands()
        {
            CommandLineBuilder builder = new CommandLineBuilder();
            if (!String.IsNullOrEmpty(Prefix))
            {
                builder.AppendSwitch("--prefix");
                builder.AppendFileNameIfNotNull(Prefix);
            }
            if(NoPackage)
            {
                builder.AppendSwitch("--no-package");
            }
            builder.AppendTextUnquoted(String.Format(" {0}", base.GenerateCommandLineCommands()));
            return builder.ToString();
        }
    }
    #endregion

    #region Slice2PythonDependTask
    public class Slice2PythonDependTask : SliceDependTask
    {
        [Required]
        public String PythonHome
        {
            get;
            set;
        }

        public String Prefix
        {
            get;
            set;
        }

        protected override String GetGeneratedPath(ITaskItem item, String outputDir, String ext)
        {
            String generatedFileName = String.Format("{0}_ice.py ", item.GetMetadata("Filename"));
            if (!String.IsNullOrEmpty(Prefix))
            {
                generatedFileName = Prefix + generatedFileName;
            }
            return Path.Combine(outputDir, generatedFileName);
        }

        protected override ITaskItem[] GeneratedItems(ITaskItem source)
        {
            return new ITaskItem[]
                {
                    new TaskItem(GetGeneratedPath(source, OutputDir, ".py")),
                };
        }

    }
    #endregion
}
