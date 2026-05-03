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
    private static bool _isConfigured;
    private static readonly object _lock = new();

    public static void Configure()
    {
        if (_isConfigured)
        {
            return;
        }

        lock (_lock)
        {
            if (_isConfigured)
            {
                return;
            }

            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            _isConfigured = true;
        }
    }
}
