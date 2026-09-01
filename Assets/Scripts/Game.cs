using System;
using UnityEngine;

namespace WontFix
{
    // The whole game lives on one component: accumulate bugs, buy generators,
    // save/load. RequireComponent pulls GameUI along automatically, so
    // adding "Game" in the Inspector is the only wiring step this game needs.
    [RequireComponent(typeof(GameUI))]
    public class Game : MonoBehaviour
    {
        const string SaveKey = "wontfix.save";

        public double Bugs { get; private set; }
        public double LifetimeBugs { get; private set; }
        public double BugsPerSecond { get; private set; }
        public int[] Owned { get; private set; }

        public event Action Changed;

        void Awake()
        {
            Owned = new int[GameData.Generators.Length];
            Load();
        }

        void Update()
        {
            if (BugsPerSecond <= 0) return;
            Add(BugsPerSecond * Time.deltaTime);
        }

        public void Click() => Add(1);

        public double CostOf(int index) =>
            Economy.CostOf(GameData.Generators[index].baseCost, Owned[index]);

        public bool CanBuy(int index) => Bugs >= CostOf(index);

        public void Buy(int index)
        {
            var cost = CostOf(index);
            if (Bugs < cost) return;

            Bugs -= cost;
            Owned[index]++;
            RecalculateRate();
            Save();
            Changed?.Invoke();
        }

        void Add(double amount)
        {
            Bugs += amount;
            LifetimeBugs += amount;
            Changed?.Invoke();
        }

        void RecalculateRate()
        {
            double rate = 0;
            for (var i = 0; i < GameData.Generators.Length; i++)
                rate += GameData.Generators[i].bugsPerSecond * Owned[i];
            BugsPerSecond = rate;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationQuit() => Save();

        void Save()
        {
            var data = new SaveData
            {
                bugs = Bugs,
                lifetimeBugs = LifetimeBugs,
                owned = Owned,
                lastSeenUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                RecalculateRate();
                return;
            }

            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            Bugs = data.bugs;
            LifetimeBugs = data.lifetimeBugs;
            if (data.owned != null && data.owned.Length == Owned.Length)
                Owned = data.owned;

            RecalculateRate();

            if (data.lastSeenUnixSeconds > 0)
            {
                var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - data.lastSeenUnixSeconds;
                if (elapsed > 0)
                    Add(Economy.Accrue(BugsPerSecond, elapsed));
            }
        }
    }
}
