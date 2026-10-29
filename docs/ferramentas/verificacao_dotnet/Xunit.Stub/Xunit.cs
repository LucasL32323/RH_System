// Dublê mínimo do xUnit (o NuGet está bloqueado neste ambiente). Imita só a API usada pelos testes do HR-Auto.
using System.Collections;
using System.Text.RegularExpressions;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method)]
    public class FactAttribute : Attribute
    {
        public string? Skip { get; set; }
        public string? DisplayName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class TheoryAttribute : FactAttribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class InlineDataAttribute : Attribute
    {
        public InlineDataAttribute(params object?[]? data) => Data = data ?? new object?[] { null };
        public object?[] Data { get; }
    }

    public interface IClassFixture<TFixture> where TFixture : class { }

    public interface IAsyncLifetime
    {
        Task InitializeAsync();
        Task DisposeAsync();
    }

    public class XunitException : Exception
    {
        public XunitException(string mensagem) : base(mensagem) { }
    }

    public static class Assert
    {
        private static XunitException Falha(string msg) => new(msg);

        // ---------- Igualdade ----------
        internal static bool Iguais(object? a, object? b)
        {
            if (a is null || b is null) return a is null && b is null;
            if (a is string || b is string) return Equals(a, b);
            if (a is IEnumerable ea && b is IEnumerable eb)
            {
                var la = ea.Cast<object?>().ToList();
                var lb = eb.Cast<object?>().ToList();
                if (a is IDictionary || b is IDictionary || EhSet(a) || EhSet(b))
                    return la.Count == lb.Count && la.All(x => lb.Any(y => Iguais(x, y)));
                return la.Count == lb.Count && la.Zip(lb).All(p => Iguais(p.First, p.Second));
            }
            return Equals(a, b);
        }

        private static bool EhSet(object o) =>
            o.GetType().GetInterfaces().Any(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(ISet<>) || i.GetGenericTypeDefinition() == typeof(IReadOnlySet<>)));

        private static string Txt(object? o) => o switch
        {
            null => "(null)",
            string s => $"\"{s}\"",
            IEnumerable e => "[" + string.Join(", ", e.Cast<object?>().Select(Txt)) + "]",
            _ => o.ToString() ?? ""
        };

        public static void Equal<T>(T expected, T actual)
        {
            if (!Iguais(expected, actual)) throw Falha($"Assert.Equal falhou. Esperado: {Txt(expected)}  Obtido: {Txt(actual)}");
        }

        public static void Equal<T>(IEnumerable<T>? expected, IEnumerable<T>? actual)
        {
            if (!Iguais(expected, actual)) throw Falha($"Assert.Equal falhou. Esperado: {Txt(expected)}  Obtido: {Txt(actual)}");
        }

        public static void Equal(string? expected, string? actual)
        {
            if (expected != actual) throw Falha($"Assert.Equal falhou. Esperado: {Txt(expected)}  Obtido: {Txt(actual)}");
        }

        public static void Equal(double expected, double actual, int precision)
        {
            if (Math.Round(expected, precision) != Math.Round(actual, precision))
                throw Falha($"Assert.Equal falhou. Esperado: {expected}  Obtido: {actual} (precisão {precision})");
        }

        public static void Equal(decimal expected, decimal actual, int precision)
        {
            if (Math.Round(expected, precision) != Math.Round(actual, precision))
                throw Falha($"Assert.Equal falhou. Esperado: {expected}  Obtido: {actual} (precisão {precision})");
        }

        public static void NotEqual<T>(T expected, T actual)
        {
            if (Iguais(expected, actual)) throw Falha($"Assert.NotEqual falhou. Valor: {Txt(actual)}");
        }

        // ---------- Booleanos e nulos ----------
        public static void True(bool condition) { if (!condition) throw Falha("Assert.True falhou."); }
        public static void True(bool condition, string? userMessage) { if (!condition) throw Falha("Assert.True falhou. " + userMessage); }
        public static void True(bool? condition) { if (condition != true) throw Falha("Assert.True falhou."); }
        public static void True(bool? condition, string? userMessage) { if (condition != true) throw Falha("Assert.True falhou. " + userMessage); }
        public static void False(bool condition) { if (condition) throw Falha("Assert.False falhou."); }
        public static void False(bool condition, string? userMessage) { if (condition) throw Falha("Assert.False falhou. " + userMessage); }
        public static void False(bool? condition) { if (condition != false) throw Falha("Assert.False falhou."); }

        public static void Null(object? @object) { if (@object is not null) throw Falha($"Assert.Null falhou. Obtido: {Txt(@object)}"); }
        public static void NotNull(object? @object) { if (@object is null) throw Falha("Assert.NotNull falhou."); }

        public static T NotNull<T>(T? value) where T : struct =>
            value ?? throw Falha("Assert.NotNull falhou.");

        // ---------- Textos ----------
        public static void Contains(string expectedSubstring, string? actualString)
        {
            if (actualString is null || !actualString.Contains(expectedSubstring))
                throw Falha($"Assert.Contains falhou. Procurado: {Txt(expectedSubstring)}  Em: {Txt(actualString)}");
        }

        public static void Contains(string expectedSubstring, string? actualString, StringComparison comparisonType)
        {
            if (actualString is null || !actualString.Contains(expectedSubstring, comparisonType))
                throw Falha($"Assert.Contains falhou. Procurado: {Txt(expectedSubstring)}  Em: {Txt(actualString)}");
        }

        public static void DoesNotContain(string expectedSubstring, string? actualString)
        {
            if (actualString is not null && actualString.Contains(expectedSubstring))
                throw Falha($"Assert.DoesNotContain falhou. Encontrado: {Txt(expectedSubstring)}  Em: {Txt(actualString)}");
        }

        public static void DoesNotContain(string expectedSubstring, string? actualString, StringComparison comparisonType)
        {
            if (actualString is not null && actualString.Contains(expectedSubstring, comparisonType))
                throw Falha($"Assert.DoesNotContain falhou. Encontrado: {Txt(expectedSubstring)}  Em: {Txt(actualString)}");
        }

        public static void StartsWith(string? expectedStartString, string? actualString)
        {
            if (actualString is null || expectedStartString is null || !actualString.StartsWith(expectedStartString, StringComparison.Ordinal))
                throw Falha($"Assert.StartsWith falhou. Esperado início: {Txt(expectedStartString)}  Obtido: {Txt(actualString)}");
        }

        public static void EndsWith(string? expectedEndString, string? actualString)
        {
            if (actualString is null || expectedEndString is null || !actualString.EndsWith(expectedEndString, StringComparison.Ordinal))
                throw Falha($"Assert.EndsWith falhou. Esperado fim: {Txt(expectedEndString)}  Obtido: {Txt(actualString)}");
        }

        public static void Matches(string expectedRegexPattern, string? actualString)
        {
            if (actualString is null || !Regex.IsMatch(actualString, expectedRegexPattern))
                throw Falha($"Assert.Matches falhou. Padrão: {expectedRegexPattern}  Obtido: {Txt(actualString)}");
        }

        // ---------- Coleções ----------
        public static void Contains<T>(T expected, IEnumerable<T> collection)
        {
            if (!collection.Any(x => Iguais(x, expected))) throw Falha($"Assert.Contains falhou. Procurado: {Txt(expected)}  Em: {Txt(collection)}");
        }

        public static void Contains<T>(IEnumerable<T> collection, Predicate<T> filter)
        {
            if (!collection.Any(x => filter(x))) throw Falha($"Assert.Contains falhou: nenhum item atende ao filtro. Coleção: {Txt(collection)}");
        }

        public static TValue Contains<TKey, TValue>(TKey expected, IReadOnlyDictionary<TKey, TValue> collection) where TKey : notnull =>
            collection.TryGetValue(expected, out var v) ? v : throw Falha($"Assert.Contains falhou. Chave ausente: {Txt(expected)}");

        public static TValue Contains<TKey, TValue>(TKey expected, Dictionary<TKey, TValue> collection) where TKey : notnull =>
            collection.TryGetValue(expected, out var v) ? v : throw Falha($"Assert.Contains falhou. Chave ausente: {Txt(expected)}");

        public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
        {
            if (collection.Any(x => Iguais(x, expected))) throw Falha($"Assert.DoesNotContain falhou. Encontrado: {Txt(expected)}");
        }

        public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> filter)
        {
            if (collection.Any(x => filter(x))) throw Falha("Assert.DoesNotContain falhou: algum item atende ao filtro.");
        }

        public static void DoesNotContain<TKey, TValue>(TKey expected, IReadOnlyDictionary<TKey, TValue> collection) where TKey : notnull
        {
            if (collection.ContainsKey(expected)) throw Falha($"Assert.DoesNotContain falhou. Chave presente: {Txt(expected)}");
        }

        public static void Empty(IEnumerable collection)
        {
            if (collection.Cast<object?>().Any()) throw Falha($"Assert.Empty falhou. Coleção: {Txt(collection)}");
        }

        public static void NotEmpty(IEnumerable collection)
        {
            if (!collection.Cast<object?>().Any()) throw Falha("Assert.NotEmpty falhou.");
        }

        public static T Single<T>(IEnumerable<T> collection)
        {
            var l = collection.ToList();
            if (l.Count != 1) throw Falha($"Assert.Single falhou: {l.Count} itens. Coleção: {Txt(l)}");
            return l[0];
        }

        public static T Single<T>(IEnumerable<T> collection, Predicate<T> predicate)
        {
            var l = collection.Where(x => predicate(x)).ToList();
            if (l.Count != 1) throw Falha($"Assert.Single falhou: {l.Count} itens atendem ao filtro.");
            return l[0];
        }

        public static object? Single(IEnumerable collection)
        {
            var l = collection.Cast<object?>().ToList();
            if (l.Count != 1) throw Falha($"Assert.Single falhou: {l.Count} itens.");
            return l[0];
        }

        public static void All<T>(IEnumerable<T> collection, Action<T> action)
        {
            foreach (var item in collection) action(item);
        }

        // ---------- Exceções ----------
        public static T Throws<T>(Action testCode) where T : Exception
        {
            try { testCode(); }
            catch (Exception e) when (e.GetType() == typeof(T)) { return (T)e; }
            catch (Exception e) { throw Falha($"Assert.Throws falhou. Esperado {typeof(T).Name}, lançado {e.GetType().Name}: {e.Message}"); }
            throw Falha($"Assert.Throws falhou. Esperado {typeof(T).Name}, nada foi lançado.");
        }

        public static T Throws<T>(Func<object?> testCode) where T : Exception => Throws<T>(() => { testCode(); });

        public static T Throws<T>(string? paramName, Action testCode) where T : ArgumentException
        {
            var e = Throws<T>(testCode);
            if (e.ParamName != paramName) throw Falha($"Assert.Throws falhou. Parâmetro esperado {paramName}, obtido {e.ParamName}.");
            return e;
        }

        public static T ThrowsAny<T>(Action testCode) where T : Exception
        {
            try { testCode(); }
            catch (T e) { return e; }
            catch (Exception e) { throw Falha($"Assert.ThrowsAny falhou. Esperado {typeof(T).Name}, lançado {e.GetType().Name}: {e.Message}"); }
            throw Falha($"Assert.ThrowsAny falhou. Esperado {typeof(T).Name}, nada foi lançado.");
        }

        public static async Task<T> ThrowsAsync<T>(Func<Task> testCode) where T : Exception
        {
            try { await testCode(); }
            catch (Exception e) when (e.GetType() == typeof(T)) { return (T)e; }
            catch (Exception e) { throw Falha($"Assert.ThrowsAsync falhou. Esperado {typeof(T).Name}, lançado {e.GetType().Name}: {e.Message}"); }
            throw Falha($"Assert.ThrowsAsync falhou. Esperado {typeof(T).Name}, nada foi lançado.");
        }

        public static async Task<T> ThrowsAnyAsync<T>(Func<Task> testCode) where T : Exception
        {
            try { await testCode(); }
            catch (T e) { return e; }
            catch (Exception e) { throw Falha($"Assert.ThrowsAnyAsync falhou. Esperado {typeof(T).Name}, lançado {e.GetType().Name}: {e.Message}"); }
            throw Falha($"Assert.ThrowsAnyAsync falhou. Esperado {typeof(T).Name}, nada foi lançado.");
        }

        public static T IsType<T>(object? @object)
        {
            if (@object is null || @object.GetType() != typeof(T)) throw Falha($"Assert.IsType falhou. Esperado {typeof(T).Name}, obtido {@object?.GetType().Name ?? "null"}.");
            return (T)@object;
        }

        public static T IsAssignableFrom<T>(object? @object)
        {
            if (@object is not T t) throw Falha($"Assert.IsAssignableFrom falhou. Esperado {typeof(T).Name}.");
            return t;
        }

        public static void Same(object? expected, object? actual) { if (!ReferenceEquals(expected, actual)) throw Falha("Assert.Same falhou."); }
        public static void NotSame(object? expected, object? actual) { if (ReferenceEquals(expected, actual)) throw Falha("Assert.NotSame falhou."); }
        public static void InRange<T>(T actual, T low, T high) where T : IComparable
        {
            if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0) throw Falha($"Assert.InRange falhou: {actual} fora de [{low}, {high}].");
        }
    }
}
