using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace SharedKernel.Infrastructure.Persistence;

/// <summary>
///     Centralizes process-wide MongoDB serializer registration so Mongo conventions are
///     configured once for the entire application instead of per module.
/// </summary>
public static class MongoSerializationConfiguration
{
    public static void Configure()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        ConventionPack pack = new() { new CamelCaseElementNameConvention() };
        ConventionRegistry.Register("CamelCase", pack, _ => true);
    }
}
