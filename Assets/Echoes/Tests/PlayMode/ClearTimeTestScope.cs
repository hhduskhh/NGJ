using System;
using UnityEngine;

namespace Echoes.Tests
{
    internal sealed class ClearTimeTestScope : IDisposable
    {
        private readonly int previous = RunController.BestClearCentiseconds;

        public ClearTimeTestScope(int initial = -1) => SetRecord(initial);

        public void Dispose() => SetRecord(previous);

        private static void SetRecord(int value)
        {
            if (value < 0) PlayerPrefs.DeleteKey(RunController.BestClearTimeKey);
            else PlayerPrefs.SetInt(RunController.BestClearTimeKey, value);
            PlayerPrefs.Save();
        }
    }
}
