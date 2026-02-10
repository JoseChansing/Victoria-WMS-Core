using System;
using System.Collections.Generic;

namespace Victoria.Core.Models
{
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public long TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;

        public PagedResult(IEnumerable<T> items, long totalItems, int page, int pageSize)
        {
            Items = items;
            TotalItems = totalItems;
            Page = page;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        }
    }
}
