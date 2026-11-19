using System;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class CondensedMatterTests
    {
        public static void RunAll()
        {
            Run("Condensed matter lattice and band models", TestModels);
            Run("Condensed matter validation", TestValidation);
        }

        private static void TestModels()
        {
            AssertEqual(CondensedMatter.UnitCellMassDensity(2.0, 0.5), 4.0, "unit-cell density");
            AssertEqual(CondensedMatter.CubicInterplanarSpacing(2.0, 1, 1, 0), Math.Sqrt(2.0), "cubic spacing");
            AssertEqual(CondensedMatter.BraggAngle(1.0, 2.0, 1), Math.Asin(0.25), "Bragg angle");
            Vector3 reciprocal = CondensedMatter.ReciprocalLatticeVector(new Vector3(1.0, 0.0, 0.0), new Vector3(0.0, 1.0, 0.0), new Vector3(0.0, 0.0, 1.0));
            AssertEqual(reciprocal.X, 2.0 * Math.PI, "reciprocal lattice vector");
            Vector3 reciprocalSecond = CondensedMatter.ReciprocalLatticeVector(
                new Vector3(1.0, 0.0, 0.0), new Vector3(0.0, 1.0, 0.0), new Vector3(0.0, 0.0, 1.0), 0, 1, 0);
            AssertEqual(reciprocalSecond.Y, 2.0 * Math.PI, "indexed reciprocal lattice vector");
            AssertEqual(CondensedMatter.TightBindingDispersion(0.0, 1.0, 1.0, 0.0), -2.0, "tight-binding band bottom");
            AssertEqual(CondensedMatter.TightBindingGroupVelocity(1.0, 1.0, 0.0), 0.0, "band velocity at zone center");
            AssertEqual(CondensedMatter.MonatomicChainPhononFrequency(1.0, 4.0, 1.0, 0.0), 0.0, "acoustic mode at zero wave number");
            AssertEqual(CondensedMatter.FermiWaveNumber(1.0), Math.Pow(3.0 * Math.PI * Math.PI, 1.0 / 3.0), "Fermi wave number");
            Assert(CondensedMatter.FreeElectronFermiEnergy(2.0, 1.0) > CondensedMatter.FreeElectronFermiEnergy(1.0, 1.0), "Fermi energy density scaling");
            SymmetricEigenResult eigen = CondensedMatter.HamiltonianEigensystem(new double[,] { { 2.0, 0.0 }, { 0.0, 3.0 } });
            Assert(Math.Abs(eigen.Eigenvalues[0] - 2.0) < 1e-8 || Math.Abs(eigen.Eigenvalues[1] - 2.0) < 1e-8, "Hamiltonian contains first eigenvalue");
            Assert(Math.Abs(eigen.Eigenvalues[0] - 3.0) < 1e-8 || Math.Abs(eigen.Eigenvalues[1] - 3.0) < 1e-8, "Hamiltonian contains second eigenvalue");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => CondensedMatter.CubicInterplanarSpacing(1.0, 0, 0, 0), "zero Miller indices are invalid");
            AssertThrows<ArgumentOutOfRangeException>(() => CondensedMatter.BraggAngle(2.1, 1.0, 1), "impossible Bragg condition");
            AssertThrows<ArgumentException>(() => CondensedMatter.ReciprocalLatticeVector(default, default, default), "degenerate direct lattice");
        }
    }
}
