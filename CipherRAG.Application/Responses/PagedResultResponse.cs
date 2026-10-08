using System.Collections.Generic;

namespace CipherRAG.Application.Responses
{
    public class PagedResultResponse<T>
    {
        public IEnumerable<T> Items { get; init; } = System.Array.Empty<T>();
        public int TotalCount { get; init; }
        public int TotalPages { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }

        public PagedResultResponse(IEnumerable<T> items, int totalCount, int totalPages, int pageNumber, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            TotalPages = totalPages;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }
    }
}
