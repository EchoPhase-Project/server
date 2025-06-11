using MoonSharp.Interpreter;

namespace EchoPhase.Runners
{
    public class LuaRunner
    {
        private readonly HashSet<string> _bannedIdentifiers;

        public LuaRunner(IEnumerable<string> bannedIdentifiers)
        {
            _bannedIdentifiers = new HashSet<string>(bannedIdentifiers);
        }

        public DynValue Execute(string code, Dictionary<string, object>? globals = null)
        {
            // 1. Проверка на запрещённые идентификаторы
            foreach (var banned in _bannedIdentifiers)
            {
                if (code.Contains(banned, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Use of banned identifier: {banned}");
            }

            // 2. Создание скрипта
            var script = new Script(CoreModules.Preset_SoftSandbox); // безопасная среда

            // 3. Регистрация и установка глобальных переменных
            if (globals != null)
            {
                foreach (var kvp in globals)
                {
                    var value = kvp.Value;
                    if (value != null)
                    {
                        var type = value.GetType();
                        UserData.RegisterType(type); // Регистрируем тип
                        script.Globals[kvp.Key] = UserData.Create(value);
                    }
                    else
                    {
                        script.Globals[kvp.Key] = DynValue.Nil;
                    }
                }
            }

            // 4. Выполнение
            return script.DoString(code);
        }
    }
}
