namespace ProductCatalog.Domain.Repository;

/// <summary>El sentido de un criterio de orden.</summary>
public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>
/// Un criterio de orden, como lo nombra el contrato: la propiedad de la columna (<c>name</c>, <c>createdAt</c>,
/// <c>priceAmount</c> para un value object aplanado), la misma que acepta el <c>?sort=</c> de keel-spring.
/// </summary>
public sealed record SortOrder(string Property, SortDirection Direction);

/// <summary>
/// La página pedida, sin framework: el dominio la nombra y el adaptador la resuelve. La misma semántica que el
/// Pageable de Spring Data que usa keel-spring: página desde 0, tamaño ya acotado al tope del diseño y el orden en
/// prioridad (vacío: el que decida el adaptador, el id). El desempate por id lo pone siempre el adaptador.
/// </summary>
public sealed record Pageable(int Page, int Size, IReadOnlyList<SortOrder> Sort);

/// <summary>Una página de resultados con sus totales.</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, int Number, int Size, long TotalElements, int TotalPages)
{
    /// <summary>La misma página con sus elementos transformados (la del dominio → la del contrato).</summary>
    public Page<TResult> Map<TResult>(Func<T, TResult> map) => new([.. Items.Select(map)], Number, Size, TotalElements, TotalPages);
}
