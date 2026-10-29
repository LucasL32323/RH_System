// Executor mínimo: acha [Fact]/[Theory]+[InlineData] por reflexão e roda cada caso numa instância nova (como o xUnit).
using System.Reflection;

internal static class Runner
{
    private static object? Converter(object? v, Type t)
    {
        if (v is null) return null;
        var alvo = Nullable.GetUnderlyingType(t) ?? t;
        if (alvo.IsInstanceOfType(v)) return v;
        if (alvo.IsEnum) return Enum.ToObject(alvo, v);
        return Convert.ChangeType(v, alvo, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task<int> Main(string[] args)
    {
        int ok = 0, falhas = 0, pulados = 0;
        var filtro = args.FirstOrDefault();
        var tipos = typeof(Runner).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).OrderBy(t => t.FullName);
        foreach (var tipo in tipos)
        foreach (var m in tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).OrderBy(m => m.Name))
        {
            var fact = m.GetCustomAttribute<FactAttribute>();
            if (fact is null) continue;
            if (filtro is not null && !$"{tipo.Name}.{m.Name}".Contains(filtro)) continue;
            if (fact.Skip is not null) { pulados++; continue; }
            var casos = fact is TheoryAttribute
                ? m.GetCustomAttributes<InlineDataAttribute>().Select(d => d.Data).ToList()
                : new List<object?[]> { Array.Empty<object?>() };
            if (casos.Count == 0) { Console.WriteLine($"SEM DADOS  {tipo.Name}.{m.Name}"); falhas++; continue; }
            foreach (var dados in casos)
            {
                var nome = $"{tipo.Name}.{m.Name}({string.Join(", ", dados.Select(d => d ?? "null"))})";
                object? inst = null;
                try
                {
                    var ps = m.GetParameters();
                    var argv = ps.Select((p, i) => Converter(i < dados.Length ? dados[i] : p.DefaultValue, p.ParameterType)).ToArray();
                    inst = m.IsStatic ? null : Activator.CreateInstance(tipo);
                    if (inst is IAsyncLifetime al) await al.InitializeAsync();
                    var r = m.Invoke(inst, argv);
                    if (r is Task tk) await tk;
                    ok++;
                }
                catch (Exception e)
                {
                    var real = e is TargetInvocationException tie && tie.InnerException is not null ? tie.InnerException : e;
                    falhas++;
                    Console.WriteLine($"FALHOU  {nome}\n        {real.GetType().Name}: {real.Message.Split('\n')[0]}");
                }
                finally
                {
                    if (inst is IAsyncLifetime al2) await al2.DisposeAsync();
                    if (inst is IDisposable d) d.Dispose();
                }
            }
        }
        Console.WriteLine($"\nTotal: {ok + falhas} casos — {ok} passaram, {falhas} falharam, {pulados} pulados.");
        return falhas == 0 ? 0 : 1;
    }
}
