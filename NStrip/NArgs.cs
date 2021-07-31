/*
	NArgs
	The MIT License (MIT)

	Copyright(c) 2021 Bepis

	Permission is hereby granted, free of charge, to any person obtaining a copy of
	this software and associated documentation files (the "Software"), to deal in
	the Software without restriction, including without limitation the rights to
	use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
	the Software, and to permit persons to whom the Software is furnished to do so,
	subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
	FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
	COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
	IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
	CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NArgs
{
	/// <summary>
	/// Command-line argument parser.
	/// </summary>
	public static class Arguments
	{
		/// <summary>
		/// Parses arguments and constructs an <see cref="IArgumentCollection"/> object.
		/// </summary>
		/// <typeparam name="T">The type of the object to construct. Must inherit from <see cref="IArgumentCollection"/></typeparam>
		/// <param name="args">The command-line arguments to parse.</param>
		/// <returns></returns>
		public static T Parse<T>(string[] args) where T : IArgumentCollection, new()
		{
			Dictionary<CommandDefinitionAttribute, Action<string>> valueSwitches = new Dictionary<CommandDefinitionAttribute, Action<string>>();
			Dictionary<CommandDefinitionAttribute, Action<bool>> boolSwitches = new Dictionary<CommandDefinitionAttribute, Action<bool>>();

			var config = new T { Values = new List<string>() };

			var commandProps = GetCommandProperties<T>();

			foreach (var kv in commandProps)
			{
				if (kv.Value.PropertyType == typeof(bool))
				{
					boolSwitches.Add(kv.Key, x => kv.Value.SetValue(config, x));
				}
				else if (kv.Value.PropertyType == typeof(string))
				{
					valueSwitches.Add(kv.Key, x => kv.Value.SetValue(config, x));
				}
				else if (typeof(IList<string>).IsAssignableFrom(kv.Value.PropertyType))
				{
					if (kv.Value.GetValue(config) == null)
					{
						kv.Value.SetValue(config, new List<string>());
					}

					valueSwitches.Add(kv.Key, x =>
					{
						var list = (IList<string>)kv.Value.GetValue(config);
						list.Add(x);
					});
				}
			}

			CommandDefinitionAttribute previousSwitchDefinition = null;
			bool valuesOnly = false;

			foreach (string arg in args)
			{
				if (arg == "--")
				{
					// no more switches, only values
					valuesOnly = true;

					continue;
				}

				if (valuesOnly)
				{
					config.Values.Add(arg);
					continue;
				}

				if (arg.StartsWith("-")
					|| arg.StartsWith("--"))
				{
					string previousSwitch;

					if (arg.StartsWith("--"))
						previousSwitch = arg.Substring(2);
					else
						previousSwitch = arg.Substring(1);

					if (boolSwitches.Keys.TryFirst(x
						=> x.LongArg.Equals(previousSwitch, StringComparison.InvariantCultureIgnoreCase)
						|| x.ShortArg?.Equals(previousSwitch, StringComparison.InvariantCultureIgnoreCase) == true,
						out var definition))
					{
						boolSwitches[definition](true);
						previousSwitch = null;

						continue;
					}

					if (valueSwitches.Keys.TryFirst(x
						=> x.LongArg.Equals(previousSwitch, StringComparison.InvariantCultureIgnoreCase)
						|| x.ShortArg?.Equals(previousSwitch, StringComparison.InvariantCultureIgnoreCase) == true,
						out definition))
					{
						previousSwitchDefinition = definition;

						continue;
					}

					Console.WriteLine("Unrecognized command line option: " + arg);
					throw new Exception();
				}

				if (previousSwitchDefinition != null)
				{
					valueSwitches[previousSwitchDefinition](arg);
					previousSwitchDefinition = null;
				}
				else
				{
					config.Values.Add(arg);
				}
			}

			foreach (var kv in commandProps)
			{
				if (!kv.Key.Required)
					continue;

				if (kv.Value.PropertyType == typeof(string))
					if (kv.Value.GetValue(config) == null)
						throw new ArgumentException($"Required argument not provided: {kv.Key.LongArg}");

				if (kv.Value.PropertyType == typeof(IList<string>))
					if (((IList<string>)kv.Value.GetValue(config)).Count == 0)
						throw new ArgumentException($"Required argument not provided: {kv.Key.LongArg}");
			}

			return config;
		}

		/// <summary>
		/// Generates a string to be printed as console help text.
		/// </summary>
		/// <typeparam name="T">The type of the arguments object to create help instructions for. Must inherit from <see cref="IArgumentCollection"/></typeparam>
		/// <param name="copyrightText">The copyright text to add at the top, if any.</param>
		/// <param name="usageText">The usage text to add at the top, if any.</param>
		public static string PrintLongHelp<T>(string copyrightText = null, string usageText = null) where T : IArgumentCollection
		{
			var commands = GetCommandProperties<T>();

			var builder = new StringBuilder();

			if (copyrightText != null)
				builder.AppendLine(copyrightText);

			if (usageText != null)
				builder.AppendLine(usageText);

			builder.AppendLine();
			builder.AppendLine();

			foreach (var command in commands.OrderBy(x => x.Key.ShortArg ?? "zzzz").ThenBy(x => x.Key.LongArg))
			{
				var valueString = string.Empty;

				if (command.Value.PropertyType == typeof(IList<string>)
					|| command.Value.PropertyType == typeof(string))
				{
					valueString = " <value>";
				}

				string listing = command.Key.ShortArg != null
					? $"  -{command.Key.ShortArg}{valueString}, --{command.Key.LongArg}{valueString}"
					: $"  --{command.Key.LongArg}{valueString}";

				const int listingWidth = 45;
				const int descriptionWidth = 65;

				string listingWidthString = "".PadLeft(listingWidth);

				builder.Append(listing.PadRight(listingWidth));

				if (listing.Length > listingWidth - 3)
				{
					builder.AppendLine();
					builder.Append(listingWidthString);
				}

				int lineLength = 0;
				int lastIndex = 0;
				int currentIndex = 0;

				while ((currentIndex = command.Key.Description.IndexOf(' ', currentIndex + 1)) != -1)
				{
					if (currentIndex - lastIndex >= descriptionWidth)
					{
						var descriptionSubstring = command.Key.Description.Substring(lastIndex, lineLength);
						builder.AppendLine(descriptionSubstring);
						builder.Append(listingWidthString);

						lastIndex += lineLength + 1;
					}

					lineLength = currentIndex - lastIndex;
				}

				if (lineLength > 0)
				{
					var remainingSubstring = command.Key.Description.Substring(lastIndex);
					builder.AppendLine(remainingSubstring);
				}

				builder.AppendLine();
			}

			builder.AppendLine();

			return builder.ToString();
		}

		private static Dictionary<CommandDefinitionAttribute, PropertyInfo> GetCommandProperties<T>()
		{
			var commands = new Dictionary<CommandDefinitionAttribute, PropertyInfo>();

			foreach (var prop in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
			{
				var commandDef = prop.GetCustomAttribute<CommandDefinitionAttribute>();

				if (commandDef == null)
					continue;

				commands.Add(commandDef, prop);
			}

			return commands;
		}

		private static bool TryFirst<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate, out T value)
		{
			foreach (var item in enumerable)
			{
				if (predicate(item))
				{
					value = item;
					return true;
				}
			}

			value = default;
			return false;
		}
	}

	/// <summary>
	/// Specifies an object is an argument collection.
	/// </summary>
	public interface IArgumentCollection
	{
		/// <summary>
		/// A list of values that were passed in as arguments, but not associated with an option.
		/// </summary>
		IList<string> Values { get; set; }
	}

	public class CommandDefinitionAttribute : Attribute
	{
		/// <summary>
		/// The short version of an option, e.g. "-a". Optional.
		/// </summary>
		public string ShortArg { get; set; }

		/// <summary>
		/// The long version of an option, e.g. "--append". Required.
		/// </summary>
		public string LongArg { get; set; }

		/// <summary>
		/// Whether or not to fail parsing if this argument has not been provided.
		/// </summary>
		public bool Required { get; set; } = false;

		/// <summary>
		/// The description of the option, to be used in the help text.
		/// </summary>
		public string Description { get; set; } = null;

		/// <param name="longArg">The long version of an option, e.g. "--append".</param>
		public CommandDefinitionAttribute(string longArg)
		{
			LongArg = longArg;
		}

		/// <param name="shortArg">The short version of an option, e.g. "-a".</param>
		/// <param name="longArg">The long version of an option, e.g. "--append".</param>
		public CommandDefinitionAttribute(string shortArg, string longArg)
		{
			ShortArg = shortArg;
			LongArg = longArg;
		}
	}
}