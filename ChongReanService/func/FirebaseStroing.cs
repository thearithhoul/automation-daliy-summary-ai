using Google.Cloud.Firestore;

namespace ChongReanProject.Func;

// Generic Firestore CRUD helper, so callers don't need to touch FirestoreDb
// directly for straightforward collection/document access (see AuthService
// for an example of talking to Firestore directly when a call needs more
// control, e.g. Timestamp fields).
public class FirebaseStroing(FirestoreDb firestoreDb)
{
    // Read a single document. Returns null if it doesn't exist.
    public async Task<T?> ReadAsync<T>(string collection, string documentId) where T : class
    {
        var snapshot = await firestoreDb.Collection(collection).Document(documentId).GetSnapshotAsync();
        return snapshot.Exists ? snapshot.ConvertTo<T>() : null;
    }

    // Read every document in a collection.
    public async Task<List<T>> ReadAllAsync<T>(string collection) where T : class
    {
        var snapshot = await firestoreDb.Collection(collection).GetSnapshotAsync();
        return snapshot.Documents.Select(doc => doc.ConvertTo<T>()).ToList();
    }

    // Write (create or overwrite) a document at a known id.
    public Task WriteAsync<T>(string collection, string documentId, T data) where T : class
    {
        return firestoreDb.Collection(collection).Document(documentId).SetAsync(data);
    }

    // Write a new document with an auto-generated id. Returns that id.
    public async Task<string> WriteAsync<T>(string collection, T data) where T : class
    {
        var docRef = await firestoreDb.Collection(collection).AddAsync(data);
        return docRef.Id;
    }

    // Merge the given fields into an existing document, leaving the rest untouched.
    public Task UpdateAsync(string collection, string documentId, IDictionary<string, object> updates)
    {
        return firestoreDb.Collection(collection).Document(documentId).UpdateAsync(updates);
    }

    // Delete a single document.
    public Task DeleteAsync(string collection, string documentId)
    {
        return firestoreDb.Collection(collection).Document(documentId).DeleteAsync();
    }

    // Delete every document in a collection. Firestore has no native
    // "drop collection" call, so this pages through and batch-deletes.
    public async Task DeleteCollectionAsync(string collection, int batchSize = 100)
    {
        var query = firestoreDb.Collection(collection).Limit(batchSize);
        while (true)
        {
            var snapshot = await query.GetSnapshotAsync();
            if (snapshot.Count == 0)
            {
                return;
            }

            var batch = firestoreDb.StartBatch();
            foreach (var doc in snapshot.Documents)
            {
                batch.Delete(doc.Reference);
            }

            await batch.CommitAsync();

            if (snapshot.Count < batchSize)
            {
                return;
            }
        }
    }
}
