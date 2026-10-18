using System.Reflection;
using Content.IntegrationTests;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using NUnit.Framework.Api;
using Robust.UnitTesting.Pool;

namespace Content.YAMLLinter
{
    internal static class Program
    {
        private static readonly List<string> LogBuffer = new();
        private static string ResultsPath => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "yaml_lint_results.txt");

        private static void Out(string message)
        {
            Console.WriteLine(message);
            LogBuffer.Add(message);
        }

        private static void FlushLog()
        {
            try
            {
                var path = ResultsPath;
                var full = Path.GetFullPath(path);
                File.WriteAllLines(full, LogBuffer);
            }
            catch
            {
                // best-effort; ignore IO errors
            }
        }

        private static async Task<int> Main(string[] _)
        {
            PoolManager.Startup();
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // HL: Manually build the test layout because this testing system is a bit jank
            var builder = new DefaultTestAssemblyBuilder();
            var settings = new Dictionary<string, object>();
            builder.Build(Assembly.GetExecutingAssembly(), settings);

            var (errors, fieldErrors) = await RunValidation();

            var count = errors.Count + fieldErrors.Count;

            if (count == 0)
            {
                Out($"No errors found in {(int)stopwatch.Elapsed.TotalMilliseconds} ms.");
                PoolManager.Shutdown();
                FlushLog();
                return 0;
            }

            foreach (var (file, errorHashset) in errors)
            {
                foreach (var errorNode in errorHashset)
                {
                    Out($"::error file={file},line={errorNode.Node.Start.Line},col={errorNode.Node.Start.Column}::{file}({errorNode.Node.Start.Line},{errorNode.Node.Start.Column})  {errorNode.ErrorReason}");
                }
            }

            foreach (var error in fieldErrors)
            {
                Out(error);
            }

            Out($"{count} errors found in {(int)stopwatch.Elapsed.TotalMilliseconds} ms.");
            PoolManager.Shutdown();
            FlushLog();
            return -1;
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static IEnumerable<Type> GetRelevantTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && a.GetName().Name is { } name &&
                            (name.StartsWith("Content") || name.StartsWith("Robust")))
                .SelectMany(GetTypesSafe);
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.OfType<Type>();
            }
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static MemberInfo[] GetMembersSafe(Type type, BindingFlags flags)
        {
            try
            {
                return type.GetMembers(flags);
            }
            catch
            {
                return [];
            }
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static string? EvaluateMember(Type type, MemberInfo member)
        {
            try
            {
                return member switch
                {
                    FieldInfo field => field.GetValue(field.IsStatic ? null : Activator.CreateInstance(type)) as string,
                    PropertyInfo prop => prop.GetValue(prop.GetMethod?.IsStatic == true ? null : Activator.CreateInstance(type)) as string,
                    MethodInfo method when method.GetParameters().Length == 0 =>
                        method.Invoke(method.IsStatic ? null : Activator.CreateInstance(type), null) as string,
                    _ => null,
                };
            }
            catch
            {
                return null;
            }
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static List<string> DiscoverTestPrototypes()
        {
            const BindingFlags flags = BindingFlags.Static
                                       | BindingFlags.Instance
                                       | BindingFlags.Public
                                       | BindingFlags.NonPublic
                                       | BindingFlags.DeclaredOnly;

            return GetRelevantTypes()
                .SelectMany(t => GetMembersSafe(t, flags).Select(m => (Type: t, Member: m)))
                .Where(x => x.Member.IsDefined(typeof(TestPrototypesAttribute), inherit: false))
                .Select(x => EvaluateMember(x.Type, x.Member))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList()!;
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static (string Name, Type Type)? TryGetPrototypeKind(Type type)
        {
            try
            {
                if (type.GetCustomAttribute<PrototypeAttribute>() is not { } attr)
                    return null;

                return (attr.Type ?? PrototypeUtility.CalculatePrototypeName(type.Name), type);
            }
            catch
            {
                return null;
            }
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static Dictionary<string, Type> GetPrototypeKindTypes()
        {
            return GetRelevantTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPrototype).IsAssignableFrom(t))
                .Select(TryGetPrototypeKind)
                .OfType<(string Name, Type Type)>()
                .GroupBy(x => x.Name, x => x.Type)
                .ToDictionary(g => g.Key, g => g.Last());
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static IEnumerable<MappingDataNode> ParseMappingNodes(string yaml)
        {
            try
            {
                using var reader = new StringReader(yaml);
                return DataNodeParser.ParseYamlStream(reader)
                    .Select(doc => doc.Root)
                    .OfType<SequenceDataNode>()
                    .SelectMany(seq => seq.OfType<MappingDataNode>())
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static Dictionary<Type, HashSet<string>> ParseTestPrototypes(
            IEnumerable<string> protoStrings,
            Dictionary<string, Type> kindNames)
        {
            return protoStrings
                .SelectMany(ParseMappingNodes)
                .Select(node =>
                {
                    node.TryGet<ValueDataNode>("type", out var typeNode);
                    node.TryGet<ValueDataNode>("id", out var idNode);
                    return (Type: typeNode?.Value, Id: idNode?.Value);
                })
                .Where(x => x is { Type: not null, Id: not null } && kindNames.ContainsKey(x.Type))
                .GroupBy(x => kindNames[x.Type!], x => x.Id!)
                .ToDictionary(g => g.Key, g => g.ToHashSet());
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static Dictionary<Type, HashSet<string>> LoadTestPrototypes(
            IPrototypeManager protoMan,
            IEnumerable<string> testPrototypeStrings)
        {
            var changed = new Dictionary<Type, HashSet<string>>();

            foreach (var str in testPrototypeStrings)
                try
                {
                    protoMan.LoadString(str, overwrite: true, changed: changed);
                }
                catch
                {
                    // Best-effort loading for prototype kinds not registered on this instance
                }

            if (changed.Count <= 0)
                return changed;
            try
            {
                protoMan.ReloadPrototypes(changed);
            }
            catch
            {
                // Best-effort reload
            }

            return changed;
        }

        /// <remarks>
        /// HardLight: Load Testing Prototype Strings
        /// </remarks>
        private static void MergeSets<TKey, TValue>(
            Dictionary<TKey, HashSet<TValue>> target,
            Dictionary<TKey, HashSet<TValue>> source)
            where TKey : notnull
        {
            foreach (var (key, values) in source)
            {
                if (target.TryGetValue(key, out var existing))
                    existing.UnionWith(values);
                else
                    target[key] = new HashSet<TValue>(values);
            }
        }

        private static async Task<(Dictionary<string, HashSet<ErrorNode>> YamlErrors, List<string> FieldErrors, Assembly[] Assemblies)>
            ValidateClient(IReadOnlyCollection<string> testPrototypeStrings, Dictionary<Type, HashSet<string>> testPrototypes)
        {
            await using var pair = await PoolManager.GetServerClient();
            var result = await ValidateInstance(pair.Client, testPrototypeStrings, testPrototypes);
            await pair.CleanReturnAsync();
            return result;
        }

        private static async Task<(Dictionary<string, HashSet<ErrorNode>> YamlErrors, List<string> FieldErrors, Assembly[] Assemblies)>
            ValidateServer(IReadOnlyCollection<string> testPrototypeStrings, Dictionary<Type, HashSet<string>> testPrototypes)
        {
            await using var pair = await PoolManager.GetServerClient();
            var result = await ValidateInstance(pair.Server, testPrototypeStrings, testPrototypes);
            await pair.CleanReturnAsync();
            return result;
        }

        private static async Task<(Dictionary<string, HashSet<ErrorNode>>, List<string>, Assembly[])> ValidateInstance(
            RobustIntegrationTest.IntegrationInstance instance,
            IReadOnlyCollection<string> testPrototypeStrings,
            Dictionary<Type, HashSet<string>> testPrototypes)
        {
            var protoMan = instance.ResolveDependency<IPrototypeManager>();
            var refl = instance.ResolveDependency<IReflectionManager>();
            Dictionary<string, HashSet<ErrorNode>> yamlErrors = default!;
            List<string> fieldErrors = default!;
            Assembly[] assemblies = default!;

            await instance.WaitPost(() =>
            {
                var changed = LoadTestPrototypes(protoMan, testPrototypeStrings);

                var engineErrors = protoMan.ValidateDirectory(new ResPath("/EnginePrototypes"), out var engPrototypes);
                yamlErrors = protoMan.ValidateDirectory(new ResPath("/Prototypes"), out var prototypes);

                MergeSets(prototypes, engPrototypes);
                MergeSets(prototypes, testPrototypes);
                MergeSets(prototypes, changed);
                MergeSets(yamlErrors, engineErrors);

                fieldErrors = protoMan.ValidateStaticFields(prototypes);
                assemblies = refl.Assemblies.ToArray();
            });

            return (yamlErrors, fieldErrors, assemblies);
        }

        public static async Task<(Dictionary<string, HashSet<ErrorNode>> YamlErrors, List<string> FieldErrors)>
            RunValidation()
        {
            var testPrototypeStrings = DiscoverTestPrototypes();
            var kindNames = GetPrototypeKindTypes();
            var testPrototypes = ParseTestPrototypes(testPrototypeStrings, kindNames);

            var serverTask = ValidateServer(testPrototypeStrings, testPrototypes);
            var clientTask = ValidateClient(testPrototypeStrings, testPrototypes);

            await Task.WhenAll(serverTask, clientTask);

            var (serverYamlErrors, serverFieldErrors, serverAssemblies) = await serverTask;
            var (clientYamlErrors, clientFieldErrors, clientAssemblies) = await clientTask;

            var serverTypes = serverAssemblies.SelectMany(n => n.GetTypes()).Select(t => t.Name).ToHashSet();
            var clientTypes = clientAssemblies.SelectMany(n => n.GetTypes()).Select(t => t.Name).ToHashSet();

            var yamlErrors = new Dictionary<string, HashSet<ErrorNode>>();

            foreach (var (key, val) in serverYamlErrors)
            {
                // Include all server errors marked as always relevant
                var newErrors = val.Where(n => n.AlwaysRelevant).ToHashSet();

                // We include sometimes-relevant errors if they exist both for the client & server
                if (clientYamlErrors.TryGetValue(key, out var clientVal))
                    newErrors.UnionWith(val.Intersect(clientVal));

                // Include any errors that relate to server-only types
                foreach (var errorNode in val)
                {
                    if (errorNode is FieldNotFoundErrorNode fieldNotFoundNode && !clientTypes.Contains(fieldNotFoundNode.FieldType.Name))
                    {
                        newErrors.Add(errorNode);
                    }
                }

                if (newErrors.Count != 0)
                    yamlErrors[key] = newErrors;
            }

            // Next add any always-relevant client errors.
            foreach (var (key, val) in clientYamlErrors)
            {
                var newErrors = val.Where(n => n.AlwaysRelevant).ToHashSet();
                if (newErrors.Count == 0)
                    continue;

                if (yamlErrors.TryGetValue(key, out var errors))
                    errors.UnionWith(val.Where(n => n.AlwaysRelevant));
                else
                    yamlErrors[key] = newErrors;

                // Include any errors that relate to client-only types
                foreach (var errorNode in val)
                {
                    if (errorNode is FieldNotFoundErrorNode fieldNotFoundNode && !serverTypes.Contains(fieldNotFoundNode.FieldType.Name))
                    {
                        newErrors.Add(errorNode);
                    }
                }
            }

            // Finally, combine the prototype ID field errors.
            var fieldErrors = serverFieldErrors
                .Concat(clientFieldErrors)
                .Distinct()
                .ToList();

            return (yamlErrors, fieldErrors);
        }
    }
}
