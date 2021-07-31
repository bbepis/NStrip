using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using NArgs;

namespace NStrip
{
	class Program
	{
		static void LogError(string message)
		{
			var oldColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;

			Console.WriteLine(message);

			Console.ForegroundColor = oldColor;
		}

		static void LogMessage(string message)
		{
			Console.WriteLine(message);
		}

		static void Main(string[] args)
		{
            NRedirectArguments arguments = Arguments.Parse<NRedirectArguments>(args);

			if (arguments.Values.Count == 0 || arguments.Help)
			{
				LogMessage(Arguments.PrintLongHelp<NRedirectArguments>(
					"NStrip v1.1, by Bepis",
					"Usage: NStrip [options] (<.NET .exe / .dll> | <directory>) [<output assembly> | <output directory>]"));
				return;
			}

			string path = arguments.Values[0];

			string outputPath = arguments.Values.Count >= 2 ? arguments.Values[1] : null;

			var resolver = new DefaultAssemblyResolver();

			foreach (var dependency in arguments.Dependencies)
				resolver.AddSearchDirectory(dependency);

			var readerParams = new ReaderParameters()
			{
				AssemblyResolver = resolver
			};

			if (Directory.Exists(path))
			{
				resolver.AddSearchDirectory(path);

				foreach (var file in Directory.EnumerateFiles(path, "*.dll"))
				{
					string fileOutputPath = outputPath != null
						? Path.Combine(outputPath, Path.GetFileName(file))
						: file;

					if (!arguments.Overwrite && outputPath == null)
						fileOutputPath = AppendToEndOfFileName(file, "-nstrip");

					StripAssembly(file, fileOutputPath, arguments.NoStrip, arguments.Public,
						arguments.KeepResources, arguments.StripType,arguments.Blacklist, readerParams);
				}
			}
			else if (File.Exists(path))
			{
				resolver.AddSearchDirectory(Path.GetDirectoryName(path));

				string fileOutputPath = outputPath ??
				                        (arguments.Overwrite ? path : AppendToEndOfFileName(path, "-nstrip"));

				StripAssembly(path, fileOutputPath, arguments.NoStrip, arguments.Public,
					arguments.KeepResources, arguments.StripType, arguments.Blacklist, readerParams);
			}
			else
			{
				LogError($"Could not find path {path}");
			}

			LogMessage("Finished!");
		}

		static void StripAssembly(string assemblyPath, string outputPath, bool noStrip, bool makePublic, bool keepResources, StripType stripType, IList<string> typeNameBlacklist, ReaderParameters readerParams)
		{
			LogMessage($"Stripping {assemblyPath}");
			using var memoryStream = new MemoryStream(File.ReadAllBytes(assemblyPath));
			using var assemblyDefinition = AssemblyDefinition.ReadAssembly(memoryStream, readerParams);

			if (!noStrip)
				AssemblyStripper.StripAssembly(assemblyDefinition, stripType, keepResources);

			if (makePublic)
				AssemblyStripper.MakePublic(assemblyDefinition, typeNameBlacklist);

			// We write to a memory stream first to ensure that Mono.Cecil doesn't have any errors when producing the assembly.
			// Otherwise, if we're overwriting the same assembly and it fails, it will overwrite with a 0 byte file

			using var tempStream = new MemoryStream();

			assemblyDefinition.Write(tempStream);

			if (noStrip && !makePublic)
				return;

			tempStream.Position = 0;
			using var outputStream = File.Open(outputPath, FileMode.Create);

			tempStream.CopyTo(outputStream);
		}

		static string AppendToEndOfFileName(string path, string appendedString)
		{
			return Path.Combine(
				Path.GetDirectoryName(path),
				$"{Path.GetFileNameWithoutExtension(path)}{appendedString}{Path.GetExtension(path)}"
			);
		}

        private class NRedirectArguments : IArgumentCollection
        {
            public IList<string> Values { get; set; }
			
            [CommandDefinition("h", "help", Description = "Prints help text", Order = 1)]
            public bool Help { get; set; }

            [CommandDefinition("p", "public", Description = "Changes visibility of all types, nested types, methods and fields to public.")]
            public bool Public { get; set; }

            [CommandDefinition("d", "dependencies", Description = "A folder that contains dependency libraries for the target assembly/assemblies. Add this if the assembly you're working on does not have all of it's dependencies in the same folder. Can be specified multiple times.")]
            public IList<string> Dependencies { get; set; }

            [CommandDefinition("b", "blacklist", Description = "Specify this to blacklist specific short type names from being publicized if you're encountering issues with types conflicting. Can be specified multiple times.")]
            public IList<string> Blacklist { get; set; }

            [CommandDefinition("n", "no-strip", Description = "Does not strip assemblies. If this is not being used with --public, assemblies are not modified/saved.")]
            public bool NoStrip { get; set; }

            [CommandDefinition("o", "overwrite", Description = "Instead of appending \"-nstrip\" to the output assembly name, overwrite the file in-place. Does nothing if an output file/directory is specified, as \"-nstrip\" is not appended in the first place.")]
            public bool Overwrite { get; set; }

            [CommandDefinition("keep-resources", Description = "Keeps manifest resources intact instead of removing them when stripping.")]
            public bool KeepResources { get; set; }

            [CommandDefinition("t", "strip-type", Description = "The type of stripping to perform.\n\nValueRet: Returns a dummy value and ret opcode. Largest but runtime-safe. Default.\nOnlyRet: Only adds a ret opcode. Slightly smaller than ValueRet but may not be runtime-safe.\nEmptyBody: No opcodes in body. Slightly smaller again but is not runtime-safe.\nThrowNull: Makes all methods throw null. Runtime-safe and is the MS standard.\nExtern: Marks all methods as extern, and removes their bodies. Smallest size, but not runtime-safe and might not be compile-time safe.")]
            public StripType StripType { get; set; }
		}
	}
}