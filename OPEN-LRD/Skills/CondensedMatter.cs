using System;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal static class CondensedMatter
    {
        /// <summary>
        /// Computes unit cell mass density (? = massPerCell / cellVolume) from cell mass and volume.
        /// </summary>
        /// <param name="massPerCell">The mass per cell.</param>
        /// <param name="cellVolume">The cell volume.</param>
        /// <returns>The result.</returns>
        public static double UnitCellMassDensity(double massPerCell, double cellVolume)
        {
            EnsurePositive(massPerCell, nameof(massPerCell));
            EnsurePositive(cellVolume, nameof(cellVolume));
            return massPerCell / cellVolume;
        }

        /// <summary>
        /// Computes interplanar spacing (d_{hkl}) for a cubic crystal lattice with latticeConstant and Miller indices (h, k, l).
        /// </summary>
        /// <param name="latticeConstant">The lattice constant.</param>
        /// <param name="h">The h.</param>
        /// <param name="k">The k.</param>
        /// <param name="l">The l.</param>
        /// <returns>The result.</returns>
        public static double CubicInterplanarSpacing(double latticeConstant, int h, int k, int l)
        {
            EnsurePositive(latticeConstant, nameof(latticeConstant));
            double indexNorm = Math.Sqrt((double)h * h + (double)k * k + (double)l * l);
            EnsurePositive(indexNorm, nameof(h));
            return latticeConstant / indexNorm;
        }

        /// <summary>
        /// Computes the Bragg diffraction angle (?) in radians for given wavelength, interplanarSpacing, and diffraction order.
        /// </summary>
        /// <param name="wavelength">The wavelength.</param>
        /// <param name="interplanarSpacing">The interplanar spacing.</param>
        /// <param name="order">The order.</param>
        /// <returns>The result.</returns>
        public static double BraggAngle(double wavelength, double interplanarSpacing, int order)
        {
            EnsurePositive(wavelength, nameof(wavelength));
            EnsurePositive(interplanarSpacing, nameof(interplanarSpacing));
            if (order <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order), "Diffraction order must be positive.");
            }
            double sine = order * wavelength / (2.0 * interplanarSpacing);
            if (sine > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(wavelength), "Bragg condition cannot be satisfied for these inputs.");
            }
            return Math.Asin(sine);
        }

        /// <summary>
        /// Computes the reciprocal lattice vector G_{hkl} for Miller indices (h, k, l) from direct lattice basis vectors first, second, and third.
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="second">The second.</param>
        /// <param name="third">The third.</param>
        /// <returns>The result.</returns>
        public static Vector3 ReciprocalLatticeVector(Vector3 first, Vector3 second, Vector3 third)
        {
            EnsureFinite(first, nameof(first));
            EnsureFinite(second, nameof(second));
            EnsureFinite(third, nameof(third));
            Vector3 cross = Math_2.CalculateCrossProduct(second, third);
            double volume = Math_2.CalculateDotProduct(first, cross);
            if (Math.Abs(volume) <= 1e-15)
            {
                throw new ArgumentException("Direct-lattice vectors must span a nonzero volume.");
            }
            return Math_2.ScaleVector(cross, 2.0 * Math.PI / volume);
        }

        /// <summary>
        /// </summary>
        /// <param name="first">The first direct-lattice vector.</param>
        /// <param name="second">The second direct-lattice vector.</param>
        /// <param name="third">The third direct-lattice vector.</param>
        /// <returns>The reciprocal lattice vectors.</returns>
        public static (Vector3 First, Vector3 Second, Vector3 Third) ReciprocalLatticeVectors(Vector3 first, Vector3 second, Vector3 third)
        {
            EnsureFinite(first, nameof(first));
            EnsureFinite(second, nameof(second));
            EnsureFinite(third, nameof(third));
            double volume = Math_2.CalculateDotProduct(first, Math_2.CalculateCrossProduct(second, third));
            if (Math.Abs(volume) <= 1e-15)
            {
                throw new ArgumentException("Direct-lattice vectors must span a nonzero volume.");
            }
            double scale = 2.0 * Math.PI / volume;
            return (
                Math_2.ScaleVector(Math_2.CalculateCrossProduct(second, third), scale),
                Math_2.ScaleVector(Math_2.CalculateCrossProduct(third, first), scale),
                Math_2.ScaleVector(Math_2.CalculateCrossProduct(first, second), scale));
        }

        /// <summary>
        /// Computes the reciprocal lattice vector G_{hkl} for Miller indices (h, k, l) from direct lattice basis vectors first, second, and third.
        /// </summary>
        /// <param name="first">The first.</param>
        /// <param name="second">The second.</param>
        /// <param name="third">The third.</param>
        /// <param name="h">The h.</param>
        /// <param name="k">The k.</param>
        /// <param name="l">The l.</param>
        /// <returns>The result.</returns>
        public static Vector3 ReciprocalLatticeVector(Vector3 first, Vector3 second, Vector3 third, int h, int k, int l)
        {
            (Vector3 reciprocalFirst, Vector3 reciprocalSecond, Vector3 reciprocalThird) = ReciprocalLatticeVectors(first, second, third);
            return (reciprocalFirst * h) + (reciprocalSecond * k) + (reciprocalThird * l);
        }

        /// <summary>
        /// Computes the 1D tight-binding energy dispersion relation E(k) from onsiteEnergy, hoppingEnergy, latticeConstant, and waveNumber k.
        /// </summary>
        /// <param name="onsiteEnergy">The onsite energy.</param>
        /// <param name="hoppingEnergy">The hopping energy.</param>
        /// <param name="latticeConstant">The lattice constant.</param>
        /// <param name="waveNumber">The wave number.</param>
        /// <returns>The result.</returns>
        public static double TightBindingDispersion(double onsiteEnergy, double hoppingEnergy, double latticeConstant, double waveNumber)
        {
            EnsureFinite(onsiteEnergy, nameof(onsiteEnergy));
            EnsureFinite(hoppingEnergy, nameof(hoppingEnergy));
            EnsurePositive(latticeConstant, nameof(latticeConstant));
            EnsureFinite(waveNumber, nameof(waveNumber));
            return onsiteEnergy - 2.0 * hoppingEnergy * Math.Cos(waveNumber * latticeConstant);
        }

        /// <summary>
        /// Computes the 1D tight-binding group velocity (v_g(k) = (1 / ?) (dE/dk)) at waveNumber k.
        /// </summary>
        /// <param name="hoppingEnergy">The hopping energy.</param>
        /// <param name="latticeConstant">The lattice constant.</param>
        /// <param name="waveNumber">The wave number.</param>
        /// <returns>The result.</returns>
        public static double TightBindingGroupVelocity(double hoppingEnergy, double latticeConstant, double waveNumber)
        {
            EnsureFinite(hoppingEnergy, nameof(hoppingEnergy));
            EnsurePositive(latticeConstant, nameof(latticeConstant));
            EnsureFinite(waveNumber, nameof(waveNumber));
            return 2.0 * hoppingEnergy * latticeConstant * Math.Sin(waveNumber * latticeConstant)
                / PhysicalConstants.ReducedPlanckConstant.ValueSI;
        }

        /// <summary>
        /// Computes the acoustic phonon angular frequency ?(k) of a 1D monatomic chain at waveNumber k.
        /// </summary>
        /// <param name="mass">The mass.</param>
        /// <param name="springConstant">The spring constant.</param>
        /// <param name="latticeConstant">The lattice constant.</param>
        /// <param name="waveNumber">The wave number.</param>
        /// <returns>The result.</returns>
        public static double MonatomicChainPhononFrequency(double mass, double springConstant, double latticeConstant, double waveNumber)
        {
            EnsurePositive(mass, nameof(mass));
            EnsurePositive(springConstant, nameof(springConstant));
            EnsurePositive(latticeConstant, nameof(latticeConstant));
            EnsureFinite(waveNumber, nameof(waveNumber));
            return 2.0 * Math.Sqrt(springConstant / mass) * Math.Abs(Math.Sin(waveNumber * latticeConstant / 2.0));
        }

        /// <summary>
        /// Computes the Fermi wave number (k_F) for a free-electron gas with given numberDensity.
        /// </summary>
        /// <param name="numberDensity">The number density.</param>
        /// <returns>The result.</returns>
        public static double FermiWaveNumber(double numberDensity)
        {
            EnsurePositive(numberDensity, nameof(numberDensity));
            return Math.Pow(3.0 * Math.PI * Math.PI * numberDensity, 1.0 / 3.0);
        }

        /// <summary>
        /// Computes the Fermi energy (E_F) of a 3D free-electron gas from numberDensity and particleMass.
        /// </summary>
        /// <param name="numberDensity">The number density.</param>
        /// <param name="particleMass">The particle mass.</param>
        /// <returns>The result.</returns>
        public static double FreeElectronFermiEnergy(double numberDensity, double particleMass)
        {
            EnsurePositive(particleMass, nameof(particleMass));
            double waveNumber = FermiWaveNumber(numberDensity);
            double hbar = PhysicalConstants.ReducedPlanckConstant.ValueSI;
            return hbar * hbar * waveNumber * waveNumber / (2.0 * particleMass);
        }

        /// <summary>
        /// Computes eigenvalues and eigenvectors of a hamiltonian matrix.
        /// </summary>
        /// <param name="hamiltonian">The hamiltonian.</param>
        /// <returns>The result.</returns>
        public static SymmetricEigenResult HamiltonianEigensystem(double[,] hamiltonian)
        {
            if (hamiltonian == null)
            {
                throw new ArgumentNullException(nameof(hamiltonian));
            }
            if (hamiltonian.GetLength(0) == 0 || hamiltonian.GetLength(0) != hamiltonian.GetLength(1))
            {
                throw new ArgumentException("Hamiltonian must be a non-empty square matrix.", nameof(hamiltonian));
            }
            if (!LinearAlgebraAdvanced.IsSymmetric(hamiltonian))
            {
                throw new ArgumentException("The real-valued Hamiltonian must be symmetric.", nameof(hamiltonian));
            }
            for (int row = 0; row < hamiltonian.GetLength(0); row++)
            {
                for (int column = 0; column < hamiltonian.GetLength(1); column++)
                {
                    if (!double.IsFinite(hamiltonian[row, column]))
                    {
                        throw new ArgumentOutOfRangeException(nameof(hamiltonian), "Hamiltonian entries must be finite.");
                    }
                }
            }
            return LinearAlgebraAdvanced.SymmetricEigenDecomposition(hamiltonian);
        }

        private static void EnsurePositive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
            }
        }

        private static void EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }

        private static void EnsureFinite(Vector3 vector, string name)
        {
            if (!double.IsFinite(vector.X) || !double.IsFinite(vector.Y) || !double.IsFinite(vector.Z))
            {
                throw new ArgumentOutOfRangeException(name, "Vector components must be finite.");
            }
        }
    }
}
