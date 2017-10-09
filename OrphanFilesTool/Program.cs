using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OrphanFilesTool
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length != 2)
			{
				Console.WriteLine("Syntax is 'OrphanFilesTool <file_extension> <folder_to_process>'.");
				Console.WriteLine("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			var extension = args[0].TrimStart('.');
			var rootDirectory = args[1];
//			var rootDirectory = @"C:\work\svn\DeadCodeRemoval\windev4\minwsw";
//			var rootDirectory = @"C:\work\svn\DeadCodeRemoval\minfosStore\Common\dsecpp";
			if (!Directory.Exists(rootDirectory))
			{
				Console.WriteLine("Specified directory does not exist.");
				Console.WriteLine("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			Console.WriteLine("!!! This utility will delete '." + extension + "' files under '" + rootDirectory + "' !!!");
			Console.WriteLine("Do you want to continue? (Y to continue)...");
			var input = Console.Read();
			if (input != 'y' && input != 'Y')
			{
				Console.WriteLine("User cancelled");
				Console.WriteLine("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			ProcessDirectory(extension, rootDirectory);

			Console.WriteLine("Processing complete!");
			Console.WriteLine("Press any key to exit...");
			Console.ReadKey();
		}

		static void ProcessDirectory(string extension, string currentDirectory)
		{
			Console.WriteLine("Processing directory: " + currentDirectory);

			// Get a list of all files that are possible candidates for removal
			var allFiles = Directory.GetFiles(currentDirectory, "*." + extension, SearchOption.AllDirectories)
				.ToList()
				.Select(Path.GetFullPath)
				.ToArray();
			var candidateFiles = FilterOutExceptions(currentDirectory, allFiles).ToList();

			// Get a list of all files that are included in a vcxproj file
			var projectFiles = Directory.GetFiles(currentDirectory, "*.vcxproj", SearchOption.AllDirectories);
			var filesInProjects = new List<string>();
			foreach (var projectFile in projectFiles)
			{
				filesInProjects.AddRange(GetFilesInProject(projectFile));
			}

			// Now work out which of the candidate files is not in the list of files referenced by projects
			int orphanedFileCount = 0;
			long totalSize = 0;
			foreach (var candidateFile in candidateFiles)
			{
				if (filesInProjects.All(t => string.Compare(t, candidateFile, StringComparison.OrdinalIgnoreCase) != 0))
				{
					Console.WriteLine(candidateFile);

					var fileInfo = new FileInfo(candidateFile);
					totalSize += fileInfo.Length;
					orphanedFileCount++;

					File.Delete(candidateFile);
				}
			}

			
			Console.WriteLine("Total number of files on disk: " + allFiles.ToList().Count);
			Console.WriteLine("Number of orphaned files: " + orphanedFileCount);
			Console.WriteLine("Total length: " + (totalSize/1024) + " Kb");
			Console.WriteLine("Total length: " + (totalSize / 1024 / 1024) + " Mb");

			Console.WriteLine();
		}

		private static IEnumerable<string> FilterOutExceptions(string baseDirectory, string[] allFiles)
		{
			Console.Write("Filtering out exceptions...");

			var candidateFiles = new List<string>();

			foreach (var currentFile in allFiles)
			{
				var extension = Path.GetExtension(currentFile);
				if (string.Compare(extension, ".vcxproj", StringComparison.InvariantCultureIgnoreCase) == 0
					|| string.Compare(extension, ".user", StringComparison.InvariantCultureIgnoreCase) == 0 // .vcxproj.user
					|| string.Compare(extension, ".filters", StringComparison.InvariantCultureIgnoreCase) == 0 // .vcxproj.filters
					|| string.Compare(extension, ".ncrunchproject", StringComparison.InvariantCultureIgnoreCase) == 0)
				{
					continue;
				}

				var filePath = Path.GetDirectoryName(currentFile);

				if (filePath != null)
				{
					if (filePath.StartsWith(Path.Combine(baseDirectory, ".svn"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "3rdParty"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "AutomationCommon"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "BuildScript"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "Common"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "Documentation"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "Infrastructure"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "InstallScript"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "packages"), StringComparison.InvariantCultureIgnoreCase)
						|| filePath.StartsWith(Path.Combine(baseDirectory, "QA"), StringComparison.InvariantCultureIgnoreCase))
					{
						continue;
					}

					if (filePath.StartsWith(Path.Combine(baseDirectory, @"WINDEV4\MinfosTPI"), StringComparison.InvariantCultureIgnoreCase))
					{
						continue;
					}
				}

				candidateFiles.Add(currentFile);
			}

			Console.WriteLine("Done!");

			return candidateFiles;
		}

		private static List<string> GetFilesInProject(string projectFile)
		{
			var projectDirectory = Path.GetDirectoryName(projectFile);
			Debug.Assert(projectDirectory != null);

			var filesInProject = new List<string>();

			var doc = XDocument.Load(projectFile);
			var ns = doc.Root?.Name.Namespace;
			var itemGroups = doc.Root?.Elements(ns + "ItemGroup").ToList();

			if (itemGroups != null)
			{
				var fileItems = new List<XElement>();
				fileItems.AddRange(itemGroups.Elements(ns + "CustomBuild"));
				fileItems.AddRange(itemGroups.Elements(ns + "ClCompile"));
				fileItems.AddRange(itemGroups.Elements(ns + "ClInclude"));
				foreach (var item in fileItems)
				{
					var fileNameAttribute = item.Attribute("Include");
					if (fileNameAttribute != null)
					{
						var fileName = fileNameAttribute.Value;

						var absFileName = Path.Combine(projectDirectory, fileName);
						absFileName = Path.GetFullPath(absFileName);
						filesInProject.Add(absFileName);
					}
				}
			}

			return filesInProject;
		}
	}
}
