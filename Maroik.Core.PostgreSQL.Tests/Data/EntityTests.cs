using System.Reflection;
using Maroik.Core.PostgreSQL.Models;

namespace Maroik.Core.PostgreSQL.Tests.Data;

/// <summary>The entity classes are plain data holders: each can be created empty, and its collection navigations start as empty collections (never null).</summary>
public class EntityTests
{
    /// <summary>Every concrete class in the <c>Maroik.Core.PostgreSQL.Models</c> namespace.</summary>
    private static IEnumerable<Type> EntityTypes() =>
        typeof(Account).Assembly.GetTypes().Where(t => t.Namespace == "Maroik.Core.PostgreSQL.Models" && t is { IsClass: true, IsAbstract: false });

    /// <summary>Every entity has a public parameterless constructor (EF materializes rows through it).</summary>
    [Fact]
    public void EveryEntity_CanBeCreated()
    {
        var types = EntityTypes().ToList();

        Assert.Equal(18, types.Count);
        foreach (Type type in types)
            Assert.NotNull(Activator.CreateInstance(type));
    }

    /// <summary>Every collection navigation is initialized, so code can add to it on a new entity without a null check.</summary>
    [Fact]
    public void EveryCollectionNavigation_StartsEmpty()
    {
        foreach (Type type in EntityTypes())
        {
            object entity = Activator.CreateInstance(type)!;
            foreach (PropertyInfo property in type.GetProperties().Where(p =>
                         p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                var collection = property.GetValue(entity) as System.Collections.IEnumerable;
                Assert.True(collection != null, $"{type.Name}.{property.Name} is null on a new entity");
                Assert.Empty(collection.Cast<object>());
            }
        }
    }
}
