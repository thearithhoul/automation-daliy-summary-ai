namespace ChongReanProject.Func;

// Firestore -> Dictionary<string, object> deserializes a map field as
// Dictionary<string, object> and an array field as List<object> (recursively),
// but everything comes back statically typed as `object`. These helpers remove
// the repeated `is Dictionary<string, object>` / `is List<object>` casting when
// the document shape is dynamic and not worth a [FirestoreData] entity.
public static class FirestoreDataExtensions
{
    public static Dictionary<string, object>? AsMap(this object? value) => value as Dictionary<string, object>;
    public static List<object>? AsList(this object? value) => value as List<object>;
    public static string? AsString(this object? value) => value as string;
    public static long? AsLong(this object? value) => value as long?;
    public static double? AsDouble(this object? value) => value as double?;
    public static bool? AsBool(this object? value) => value as bool?;

    public static Dictionary<string, object>? GetMap(this IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var value) ? value.AsMap() : null;

    public static List<object>? GetList(this IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var value) ? value.AsList() : null;

    public static string? GetString(this IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var value) ? value.AsString() : null;

    public static long? GetLong(this IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var value) ? value.AsLong() : null;

    public static double? GetDouble(this IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var value) ? value.AsDouble() : null;

    public static bool? GetBool(this IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var value) ? value.AsBool() : null;

    // Convenience for the common "array of maps" shape (e.g. an `articles` field).
    public static IEnumerable<Dictionary<string, object>> AsMapList(this object? value) =>
        value.AsList()?.Select(item => item.AsMap()).OfType<Dictionary<string, object>>()
        ?? [];
}
