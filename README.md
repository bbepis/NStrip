# NStrip
.NET Assembly stripper, publicizer and general utility tool

## Usage
The general usage of NStrip is `NStrip [options] <input> <output>`. Input and output can be a file or a folder, but they have to match.

- `-h` prints help text.
- `-p` sets all types, methods, properties and fields to public.
- `-n` does not strip assemblies. If not used in conjunction with `-p`, this tool will write nothing.
- `-d <folder>` specifies a folder that contains additional dependencies for the target assemblies, if they are currently not in the same folder as the target assembly. Mono.Cecil will fail to output files if it cannot find all dependencies for the target assemblies. Can be specified multiple times
- `-b` is a blacklist for type name publicization. For example, `-b "Type"` will prevent types with the name "Type" from becoming public, which can help if types that are publicizised conflict with already public types and can cause issues with compilation.
- `-o` will overwrite target assemblies instead of appending `-nstrip` to the end of the filename. Does nothing if `<output>` is specified.
- `--keep-resources` will not strip manifest resources when stripping an assembly.
- `-t` specifies the type of unstripping that will be used:
  - `ValueRet`: Returns a dummy value and ret opcode. Largest but runtime-safe.
  - `OnlyRet`: Only adds a ret opcode. Slightly smaller than ValueRet but may not be runtime-safe.
  - `EmptyBody`: No opcodes in body. Slightly smaller again but is not runtime-safe.
  - `ThrowNull`: Makes all methods throw null. Runtime-safe and is the MS standard. Default.
  - `Extern`: Marks all methods as extern, and removes their bodies. Smallest size, but not runtime-safe and might not be compile-time safe.

## Credits
Uses NArgs from https://github.com/bbepis/NArgs
