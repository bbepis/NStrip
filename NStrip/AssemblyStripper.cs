using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NStrip
{
	public enum StripType
	{
		ThrowNull,
		ValueRet,
		OnlyRet,
		EmptyBody,
		Extern
	}

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

		static void ClearMethodBodies(TypeReference voidTypeReference, ICollection<MethodDefinition> methods, StripType stripType)
		{
			foreach (MethodDefinition method in methods)
			{
				if (!method.HasBody)
					continue;

				if (stripType == StripType.Extern)
				{
					method.Body = null;
					method.IsRuntime = true;
					method.IsIL = false;
				}
				else
				{
					MethodBody body = new MethodBody(method);
					var il = body.GetILProcessor();

					if (stripType == StripType.ValueRet)
					{
						if (method.ReturnType.IsPrimitive)
						{
							il.Emit(OpCodes.Ldc_I4_0);
						}
						else if (method.ReturnType != voidTypeReference)
						{
							il.Emit(OpCodes.Ldnull);
						}

						il.Emit(OpCodes.Ret);
					}
					else if (stripType == StripType.OnlyRet)
					{
						il.Emit(OpCodes.Ret);
					}
					else if (stripType == StripType.ThrowNull)
					{
						il.Emit(OpCodes.Ldnull);
						il.Emit(OpCodes.Throw);
					}
					else if (stripType == StripType.EmptyBody)
					{
						il.Clear();
					}

					method.Body = body;

					// Probably not necessary but just in case
					method.AggressiveInlining = false;
					method.NoInlining = true;
				}
			}
		}

		public static void StripAssembly(AssemblyDefinition assembly, StripType stripType, bool keepResources)
		{
			if (!assembly.MainModule.TryGetTypeReference("System.Void", out var voidTypeReference))
			{
				voidTypeReference = assembly.MainModule.ImportReference(typeof(void));
			}

			foreach (TypeDefinition type in GetAllTypeDefinitions(assembly))
			{
				if (type.IsEnum || type.IsInterface)
					continue;

				ClearMethodBodies(voidTypeReference, type.Methods, stripType);
			}

			if (!keepResources)
				assembly.MainModule.Resources.Clear();
		}

		public static void MakePublic(AssemblyDefinition assembly, IList<string> typeNameBlacklist, bool includeCompilerGenerated, bool excludeCgEvents)
		{
			bool checkCompilerGeneratedAttribute(IMemberDefinition member)
			{
				return member.CustomAttributes.Any(x =>
					x.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
			}

			foreach (var type in GetAllTypeDefinitions(assembly))
			{
				if (typeNameBlacklist.Contains(type.Name))
					continue;

				if (!includeCompilerGenerated && checkCompilerGeneratedAttribute(type))
					continue;

				if (type.IsNested)
					type.IsNestedPublic = true;
				else
					type.IsPublic = true;

				foreach (var method in type.Methods)
				{
					if (!includeCompilerGenerated &&
					    (checkCompilerGeneratedAttribute(method) || method.IsCompilerControlled))
						continue;

					method.IsPublic = true;
				}

				foreach (var field in type.Fields)
				{
					if (!includeCompilerGenerated &&
					    (checkCompilerGeneratedAttribute(field) || field.IsCompilerControlled))
						continue;

					if (includeCompilerGenerated && excludeCgEvents)
					{
						if (type.Events.Any(x => x.Name == field.Name))
							continue;
					}

					field.IsPublic = true;
				}
			}
		}
	}
}