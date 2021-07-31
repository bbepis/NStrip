﻿using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NStrip
{
	public static class AssemblyStripper
	{
		static IEnumerable<TypeDefinition> GetAllTypeDefinitions(AssemblyDefinition assembly)
		{
			var typeQueue = new Queue<TypeDefinition>(assembly.MainModule.Types);

			while (typeQueue.Count > 0)
			{
				var type = typeQueue.Dequeue();

				yield return type;

				foreach (var nestedType in type.NestedTypes)
					typeQueue.Enqueue(nestedType);
			}
		}

		static void ClearMethodBodies(TypeReference voidTypeReference, ICollection<MethodDefinition> methods)
		{
			foreach (MethodDefinition method in methods)
			{
				if (!method.HasBody)
					continue;

				MethodBody body = new MethodBody(method);
				var il = body.GetILProcessor();
				
				// There's multiple ways we could handle this:
				// - Only provide a ret. Smallest size, however if .NET tries to load this assembly during runtime it might fail.
				// - Providing a value and ret (what we currently do). Slightly more space, however .NET should be fine loading it.
				// - Null body, i.e. mark everything as extern. Should theoretically work when loaded into .NET and be the smallest size,
				// but the size of assembly remains the same. Might be a bug within Mono.Cecil.

				if (method.ReturnType.IsPrimitive)
				{
					il.Emit(OpCodes.Ldc_I4_0);
				}
				else if (method.ReturnType != voidTypeReference)
				{
					il.Emit(OpCodes.Ldnull);
				}

				il.Emit(OpCodes.Ret);

				method.Body = body;

				method.AggressiveInlining = false;
				method.NoInlining = true;
			}
		}

		public static void StripAssembly(AssemblyDefinition assembly)
		{
			if (!assembly.MainModule.TryGetTypeReference("System.Void", out var voidTypeReference))
			{
				voidTypeReference = assembly.MainModule.ImportReference(typeof(void));
			}

			foreach (TypeDefinition type in GetAllTypeDefinitions(assembly))
			{
				if (type.IsEnum || type.IsInterface)
					continue;

				ClearMethodBodies(voidTypeReference, type.Methods);
			}

			assembly.MainModule.Resources.Clear();
		}

		public static void MakePublic(AssemblyDefinition assembly, IList<string> typeNameBlacklist)
		{
			foreach (var type in GetAllTypeDefinitions(assembly))
			{
				if (typeNameBlacklist.Contains(type.Name))
					continue;

				if (type.IsNested)
					type.IsNestedPublic = true;
				else
					type.IsPublic = true;

				foreach (var method in type.Methods)
					method.IsPublic = true;

				foreach (var field in type.Fields)
					field.IsPublic = true;
			}
		}
	}
}