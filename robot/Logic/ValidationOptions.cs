using System;

namespace PixelDataProgramari.Logic
{
    /// <summary>
    /// Opțiunile validării. În robot, valorile vin din Config.xlsx (RequireCnp, PastToleranceMinutes).
    /// </summary>
    public sealed class ValidationOptions
    {
        /// <summary>Un CNP gol este eroare (CNP_REQUIRED). Implicit true.</summary>
        public bool RequireCnp { get; set; } = true;

        /// <summary>Cât de mult poate fi ScheduledAt în trecut față de momentul procesării. Implicit zero.</summary>
        public TimeSpan PastTolerance { get; set; } = TimeSpan.Zero;
    }
}
