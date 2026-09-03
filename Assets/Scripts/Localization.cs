using System;
using System.Collections.Generic;
using UnityEngine;

namespace WontFix
{
    public enum Language
    {
        English,
        PortugueseBR,
    }

    // Hand-rolled, mirroring GameData's "content lives in code" pattern --
    // Unity's Localization package assumes Editor-window authoring, which
    // nobody can do headlessly here. Self-initializing (not tied to any
    // MonoBehaviour's Awake) because Unity doesn't guarantee Game.Awake()
    // runs before GameUI.Awake() just because GameUI is a required
    // component -- Get()/Current resolve themselves the first time
    // anything touches them, so there's no ordering to get wrong.
    public static class Localization
    {
        const string LanguageKey = "wontfix.language";

        public static event Action Changed;

        static Language current;
        static bool initialized;

        public static Language Current
        {
            get
            {
                EnsureInitialized();
                return current;
            }
        }

        public static void SetLanguage(Language language)
        {
            EnsureInitialized();
            if (current == language) return;

            current = language;
            PlayerPrefs.SetInt(LanguageKey, (int)current);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            EnsureInitialized();
            var table = current == Language.English ? En : PtBr;
            return table.TryGetValue(key, out var value) ? value : key;
        }

        // Test-only: clears cached state so the next Get()/Current call
        // re-reads PlayerPrefs from scratch. Without this, running EditMode
        // tests and then pressing Play in the same Editor session would
        // leak whatever language the tests last set into the live game,
        // since `initialized` would already be true.
        public static void ResetForTests()
        {
            initialized = false;
        }

        static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            if (PlayerPrefs.HasKey(LanguageKey))
            {
                var stored = PlayerPrefs.GetInt(LanguageKey);
                current = Enum.IsDefined(typeof(Language), stored) ? (Language)stored : Language.English;
                return;
            }

            current = Application.systemLanguage == SystemLanguage.Portuguese
                ? Language.PortugueseBR
                : Language.English;
            PlayerPrefs.SetInt(LanguageKey, (int)current);
            PlayerPrefs.Save();
        }

        static readonly Dictionary<string, string> En = new()
        {
            ["ui.run_test"] = "RUN TEST",
            ["ui.bugs_found"] = "{0} bugs found",
            ["ui.bugs_per_second"] = "{0} bugs/sec",
            ["gen.junior_tester.name"] = "Junior Tester",
            ["gen.junior_tester.flavor"] = "Clicks every button. Occasionally reads the spec.",
            ["gen.test_case_spreadsheet.name"] = "Test Case Spreadsheet",
            ["gen.test_case_spreadsheet.flavor"] = "400 rows. Last updated 2019.",
            ["gen.selenium_script.name"] = "Selenium Script",
            ["gen.selenium_script.flavor"] = "Flaky, but it runs. Mostly.",
            ["gen.ci_pipeline.name"] = "CI Pipeline",
            ["gen.ci_pipeline.flavor"] = "Runs on every commit. Red on every commit.",
            ["gen.load_test_rig.name"] = "Load Test Rig",
            ["gen.load_test_rig.flavor"] = "Finds bugs by asking \"what if 10,000 users?\"",
            ["gen.fuzzer.name"] = "Fuzzer",
            ["gen.fuzzer.flavor"] = "Throws garbage at it until something screams.",
            ["gen.static_analyzer.name"] = "Static Analyzer",
            ["gen.static_analyzer.flavor"] = "4,000 warnings. Three of them matter.",
            ["gen.monkey_test_farm.name"] = "Monkey Test Farm",
            ["gen.monkey_test_farm.flavor"] = "Literal monkeys. Literal tablets. Great coverage.",
            ["gen.llm_test_agent.name"] = "LLM Test Agent",
            ["gen.llm_test_agent.flavor"] = "Writes brilliant tests for features you don't have.",
            ["gen.chaos_engineering_cluster.name"] = "Chaos Engineering Cluster",
            ["gen.chaos_engineering_cluster.flavor"] = "Breaks production on purpose, for science.",
            ["gen.formal_verification_lab.name"] = "Formal Verification Lab",
            ["gen.formal_verification_lab.flavor"] = "Proves mathematically that the bug exists.",
            ["gen.production_users.name"] = "Production Users",
            ["gen.production_users.flavor"] = "The finest test environment ever devised.",
        };

        static readonly Dictionary<string, string> PtBr = new()
        {
            ["ui.run_test"] = "EXECUTAR TESTE",
            ["ui.bugs_found"] = "{0} bugs encontrados",
            ["ui.bugs_per_second"] = "{0} bugs/seg",
            ["gen.junior_tester.name"] = "Testador Júnior",
            ["gen.junior_tester.flavor"] = "Clica em todos os botões. Às vezes lê a especificação.",
            ["gen.test_case_spreadsheet.name"] = "Planilha de Casos de Teste",
            ["gen.test_case_spreadsheet.flavor"] = "400 linhas. Última atualização em 2019.",
            ["gen.selenium_script.name"] = "Script Selenium",
            ["gen.selenium_script.flavor"] = "Instável, mas funciona. Quase sempre.",
            ["gen.ci_pipeline.name"] = "Pipeline de CI",
            ["gen.ci_pipeline.flavor"] = "Roda a cada commit. Fica vermelho a cada commit.",
            ["gen.load_test_rig.name"] = "Bancada de Teste de Carga",
            ["gen.load_test_rig.flavor"] = "Encontra bugs perguntando \"e se fossem 10.000 usuários?\"",
            ["gen.fuzzer.name"] = "Fuzzer",
            ["gen.fuzzer.flavor"] = "Joga lixo até algo gritar.",
            ["gen.static_analyzer.name"] = "Analisador Estático",
            ["gen.static_analyzer.flavor"] = "4.000 avisos. Três deles importam.",
            ["gen.monkey_test_farm.name"] = "Fazenda de Teste de Macaco",
            ["gen.monkey_test_farm.flavor"] = "Macacos de verdade. Tablets de verdade. Ótima cobertura.",
            ["gen.llm_test_agent.name"] = "Agente de Teste com LLM",
            ["gen.llm_test_agent.flavor"] = "Escreve testes brilhantes para funcionalidades que você não tem.",
            ["gen.chaos_engineering_cluster.name"] = "Cluster de Engenharia do Caos",
            ["gen.chaos_engineering_cluster.flavor"] = "Quebra a produção de propósito, pela ciência.",
            ["gen.formal_verification_lab.name"] = "Laboratório de Verificação Formal",
            ["gen.formal_verification_lab.flavor"] = "Prova matematicamente que o bug existe.",
            ["gen.production_users.name"] = "Usuários em Produção",
            ["gen.production_users.flavor"] = "O melhor ambiente de teste já criado.",
        };
    }
}
