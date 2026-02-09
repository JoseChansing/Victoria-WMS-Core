using System.Collections.Generic;
using Newtonsoft.Json;
using Victoria.Core;

namespace Victoria.Inventory.Domain.ValueObjects
{
    public class PhysicalAttributes : ValueObject
    {
        [JsonProperty] public double Weight { get; set; }
        [JsonProperty] public double Length { get; set; }
        [JsonProperty] public double Width { get; set; }
        [JsonProperty] public double Height { get; set; }

        [JsonConstructor]
        private PhysicalAttributes() { } // Marten

        public PhysicalAttributes(double weight, double length, double width, double height)
        {
            Weight = weight;
            Length = length;
            Width = width;
            Height = height;
        }

        public static PhysicalAttributes Create(double weight, double length, double width, double height)
            => new PhysicalAttributes(weight, length, width, height);

        public static PhysicalAttributes Empty() => new PhysicalAttributes(0, 0, 0, 0);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Weight;
            yield return Length;
            yield return Width;
            yield return Height;
        }
    }
}
