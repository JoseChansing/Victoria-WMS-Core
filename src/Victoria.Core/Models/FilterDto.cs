using System.Collections.Generic;

namespace Victoria.Core.Models
{
    public class FilterDto
    {
        public List<string> Brands { get; set; } = new();
        public List<string> Categories { get; set; } = new();
    }
}
