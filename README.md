# NStrip
.NET Assembly stripper, publicizer and general utility tool

## Usage
The general usage of NStrip is `NStrip [options] <input> (<output>)`. Input and output can be a file or a folder, but they have to match. Output is optional.

- `-h | --help` prints help text.
- `-p | --public` sets all types, methods, properties and fields to public.
- `-n | --no-strip` does not strip assemblies. If not used in conjunction with `-p`, this tool will write nothing.
- `-d <folder> | --dependencies <folder>` specifies a folder that contains additional dependencies for the target assemblies, if they are currently not in the same folder as the target assembly. Mono.Cecil will fail to output files if it cannot find all dependencies for the target assemblies. Can be specified multiple times
- `-b | --blacklist` is a blacklist for type name publicization. For example, `-b "Type"` will prevent types with the name "Type" from becoming public, which can help if types that are publicizised conflict with already public types and can cause issues with compilation.
- `-o | --overwrite` will overwrite target assemblies instead of appending `-nstrip` to the end of the filename. Does nothing if `<output>` is specified.
- `-cg | --include-compiler-generated` will publicize compiler generated members & types (they are not made public by default). `-p` is required for this to be useful.
  - `--cg-exclude-events` is used in conjunction with `-cg` if you wish to exclude event backing fields from being publicized, as they typically have the same name and can cause compilation issues.
- `--keep-resources` will not strip manifest resources when stripping an assembly.
- `-t | --strip-type` specifies the type of method body stripping that will be used:
  - `ThrowNull`: Makes all methods throw null. Runtime-safe and is the MS standard. Default.
  - `ValueRet`: Returns a dummy value and ret opcode. Largest but runtime-safe.
  - `OnlyRet`: Only adds a ret opcode. Slightly smaller than ValueRet but may not be runtime-safe.
  - `EmptyBody`: No opcodes in body. Slightly smaller again but is not runtime-safe.
  - `Extern`: Marks all methods as extern, and removes their bodies. Smallest size, but not runtime-safe and might not be compile-time safe.
- `--remove-readonly` removes the readonly attribute from fields. Only works with the mono runtime, other runtimes will complain about access violations.
- `--unity-non-serialized` prevents Unity from implicitly serializing publicized fields. For use within the Unity Editor

## Credits
Uses NArgs from https://github.com/bbepis/NArgs
