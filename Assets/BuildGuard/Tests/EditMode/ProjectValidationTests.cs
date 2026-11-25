using BuildGuard.Editor;
using NUnit.Framework;

namespace BuildGuard.Tests.EditMode
{
    /// <summary>
    /// Locks the sample project to the same rule CI enforces.
    /// The failure cases live in <see cref="BuildValidatorTests"/>; this only checks the real assets.
    /// </summary>
    public sealed class ProjectValidationTests
    {
        [Test]
        public void ThisProject_HasNoValidationErrors()
        {
            var report = BuildValidationRunner.ValidateProject();

            Assert.AreEqual(0, report.ErrorCount, report.ToJson());
        }
    }
}
