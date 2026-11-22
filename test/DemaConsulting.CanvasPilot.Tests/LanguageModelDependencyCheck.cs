// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Reflection;
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Finds the references that the types and the source files of one subsystem have to the libraries of language
///     models and agents. The application library as a whole refers to such libraries (the AI copilot is built on them),
///     so the rule that a subsystem such as Knowledge or Style stays independent of every provider is checked on the
///     subsystem's own types and files.
/// </summary>
internal static partial class LanguageModelDependencyCheck
{
    /// <summary>
    ///     The names, or the beginnings of the names, of the assemblies of language model and agent libraries.
    /// </summary>
    private static readonly string[] AssemblyPrefixes =
    [
        "Microsoft.Copilot",
        "GitHub.Copilot",
        "OllamaSharp",
        "Microsoft.Extensions.AI",
        "Microsoft.Agents",
        "DemaConsulting.AgentKit",
        "Microsoft.SemanticKernel",
        "OpenAI",
        "Azure.AI",
        "Anthropic",
        "LLamaSharp"
    ];

    /// <summary>
    ///     Finds every type that is declared in a namespace and that uses a type of a language model or agent library
    ///     in its base type, interfaces, fields, properties, events, constructors, or methods (parameters and results).
    /// </summary>
    /// <param name="assembly">The assembly that declares the types.</param>
    /// <param name="namespaceName">The namespace of the subsystem; its nested namespaces are included.</param>
    /// <returns>One line for each offending use, empty when there is none.</returns>
    public static IReadOnlyList<string> FindTypeReferences(Assembly assembly, string namespaceName)
    {
        var findings = new List<string>();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in assembly.GetTypes().Where(t => IsInNamespace(t, namespaceName)))
        {
            var used = new List<Type?> { type.BaseType };
            used.AddRange(type.GetInterfaces());
            used.AddRange(type.GetFields(all).Select(f => f.FieldType));
            used.AddRange(type.GetProperties(all).Select(p => p.PropertyType));
            used.AddRange(type.GetEvents(all).Select(e => e.EventHandlerType));
            foreach (var method in type.GetMethods(all))
            {
                used.Add(method.ReturnType);
                used.AddRange(method.GetParameters().Select(p => p.ParameterType));
            }

            foreach (var constructor in type.GetConstructors(all))
            {
                used.AddRange(constructor.GetParameters().Select(p => p.ParameterType));
            }

            foreach (var other in used.Where(u => u is not null).SelectMany(u => Flatten(u!)).Distinct())
            {
                var assemblyName = other.Assembly.GetName().Name ?? string.Empty;
                if (AssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    findings.Add($"{type.FullName} uses {other.FullName} of {assemblyName}");
                }
            }
        }

        return findings;
    }

    /// <summary>
    ///     Finds every source file of a folder that has a using directive for a namespace of a language model or agent
    ///     library.
    /// </summary>
    /// <param name="sourceFolder">The folder of the subsystem's source files, searched with its sub-folders.</param>
    /// <returns>One line for each offending directive, empty when there is none.</returns>
    public static IReadOnlyList<string> FindUsingDirectives(string sourceFolder)
    {
        var findings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(sourceFolder, "*.cs", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                var match = UsingDirective().Match(line);
                if (match.Success && AssemblyPrefixes.Any(prefix => match.Groups["name"].Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    findings.Add($"{Path.GetFileName(file)}: {line.Trim()}");
                }
            }
        }

        return findings;
    }

    /// <summary>
    ///     Gets whether a type is declared in a namespace or one of its nested namespaces.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="namespaceName">The namespace.</param>
    /// <returns><see langword="true"/> when the type is in the namespace.</returns>
    private static bool IsInNamespace(Type type, string namespaceName)
    {
        var declared = type.Namespace ?? string.Empty;
        return declared == namespaceName || declared.StartsWith(namespaceName + ".", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets a type with the element type of an array or reference and the type arguments of a generic type, all the
    ///     way down.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The type and the types that it is made of.</returns>
    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        var element = type.HasElementType ? type.GetElementType() : null;
        if (element is not null)
        {
            foreach (var inner in Flatten(element))
            {
                yield return inner;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Flatten))
            {
                yield return argument;
            }
        }
    }

    /// <summary>
    ///     Matches a using directive and captures the namespace that it names.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"^\s*(global\s+)?using\s+(static\s+)?(\w+\s*=\s*)?(?<name>[\w.]+)\s*;")]
    private static partial Regex UsingDirective();
}
