namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Lleva una colección del agregado a sus filas YA CARGADAS, en vez de borrarlas y volver a insertarlas: lo que sigue
/// igual no se toca, lo nuevo se añade y lo que ya no está se BORRA (el orphanRemoval de JPA; con una FK nula, EF solo
/// la dejaría huérfana). Es el merge que hace Hibernate con una entidad detached.
/// </summary>
public static class RowSync
{
    /// <summary>Hijas con identidad: se emparejan por su id.</summary>
    public static void Children<TRow, TEntity, TKey>(
        DbContext db,
        List<TRow> rows,
        IReadOnlyList<TEntity> entities,
        Func<TRow, TKey> rowKey,
        Func<TEntity, TKey> entityKey,
        Func<TEntity, TRow> create,
        Action<TRow, TEntity> apply)
        where TRow : class
        where TKey : notnull
    {
        var wanted = entities.Select(entityKey).ToHashSet();
        foreach (var gone in rows.Where(row => !wanted.Contains(rowKey(row))).ToList())
        {
            rows.Remove(gone);
            db.Remove(gone);
        }
        var byKey = rows.ToDictionary(rowKey);
        foreach (var entity in entities)
        {
            if (!byKey.TryGetValue(entityKey(entity), out var row))
            {
                row = create(entity);
                rows.Add(row);
            }
            apply(row, entity);
        }
    }

    /// <summary>
    /// Elementos sin identidad de una lista: se emparejan por POSICIÓN, que es parte de la clave y no se puede
    /// cambiar en una fila ya guardada. Sobran los de más; faltan, se añaden.
    /// </summary>
    public static void Elements<TRow, TValue>(
        DbContext db,
        List<TRow> rows,
        IReadOnlyList<TValue> values,
        Func<int, TRow> create,
        Action<TRow, TValue> apply)
        where TRow : class, IElementRow
    {
        foreach (var gone in rows.Where(row => row.Position < 0 || row.Position >= values.Count).ToList())
        {
            rows.Remove(gone);
            db.Remove(gone);
        }
        var byPosition = rows.ToDictionary(row => row.Position);
        for (var position = 0; position < values.Count; position++)
        {
            if (!byPosition.TryGetValue(position, out var row))
            {
                row = create(position);
                rows.Add(row);
            }
            apply(row, values[position]);
        }
    }
}
