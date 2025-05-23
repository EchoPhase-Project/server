using Wasmtime;

using EchoPhase.Interfaces;

public class WasmRunner : ITriggerRunner, IDisposable
{
    private readonly Engine _engine;
    private readonly Store _store;
    private readonly Module _module;
    private readonly Linker _linker;
    private readonly Instance _instance;
    private readonly Function _function;

    public WasmRunner(string wasmPath, string functionName = "handle")
    {
		if (String.IsNullOrWhiteSpace(wasmPath))
			throw new ArgumentNullException("Missing WASM path.");

		if (String.IsNullOrWhiteSpace(functionName))
			throw new ArgumentNullException("Blank function name.");

        if (!File.Exists(wasmPath))
            throw new FileNotFoundException("WASM module not found.", wasmPath);

        _engine = new Engine();
        _store = new Store(_engine);
        _module = Module.FromFile(_engine, wasmPath);
        _linker = new Linker(_engine);

        _linker.DefineWasi();
        _store.SetWasiConfiguration(
			new WasiConfiguration()
				.WithInheritedStandardOutput()
				.WithInheritedStandardError()
		);

        _instance = _linker.Instantiate(_store, _module);
        _function = _instance.GetFunction(functionName)
            ?? throw new InvalidOperationException($"Function '{functionName}' not found in WASM module.");
    }

    public async Task<string> HandleAsync(string input)
    {

        if (input is not string inputStr)
            throw new ArgumentException("Expected input of type string", nameof(input));

        if (_function.WrapFunc<string, string>() is not Func<string, string> func)
            throw new InvalidOperationException("Failed to wrap WASM function as (string) -> string.");

		var result = await Task.FromResult(func(inputStr));

		if (String.IsNullOrWhiteSpace(result))
			throw new NullReferenceException("No result from WASM module.");

        return result;
    }

    public void Dispose()
    {
        _store.Dispose();
        _module.Dispose();
        _engine.Dispose();
    }
}
