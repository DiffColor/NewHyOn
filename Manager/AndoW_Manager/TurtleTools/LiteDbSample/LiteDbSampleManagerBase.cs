using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LiteDB;

namespace TurtleTools.LiteDbSample
{
    public abstract class LiteDbSampleManagerBase<T>
    {
        private readonly string _collectionName;
        private readonly string _primaryKeyName;
        private readonly MemberInfo _primaryKeyMember;

        protected LiteDbSampleManagerBase(string collectionName, string primaryKeyName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name is required.", nameof(collectionName));
            }

            _collectionName = collectionName;
            _primaryKeyName = string.IsNullOrWhiteSpace(primaryKeyName) ? "_id" : primaryKeyName;
            _primaryKeyMember = ResolvePrimaryKeyMember(_primaryKeyName);

            LiteDbSampleContext.EnsureCollection<T>(_collectionName);
        }

        protected List<T> LoadAllDocuments()
        {
            return LiteDbSampleContext.Execute(db =>
                db.GetCollection<T>(_collectionName).FindAll().ToList());
        }

        protected List<T> Find(Func<T, bool> predicate)
        {
            var documents = LoadAllDocuments();
            return predicate == null ? documents : documents.Where(predicate).ToList();
        }

        protected T FindOne(Func<T, bool> predicate)
        {
            return Find(predicate).FirstOrDefault();
        }

        protected T FindById(object id)
        {
            if (id == null)
            {
                return default;
            }

            return LiteDbSampleContext.Execute(db =>
                db.GetCollection<T>(_collectionName).FindById(BsonValue.Create(id)));
        }

        protected void Upsert(T document)
        {
            if (document == null)
            {
                return;
            }

            LiteDbSampleContext.Execute(db =>
            {
                db.GetCollection<T>(_collectionName).Upsert(document);
            });
        }

        protected void InsertMany(IEnumerable<T> documents)
        {
            var list = documents?.ToList();
            if (list == null || list.Count == 0)
            {
                return;
            }

            LiteDbSampleContext.Execute(db =>
            {
                db.GetCollection<T>(_collectionName).Upsert(list);
            });
        }

        protected void ReplaceAllDocuments(IEnumerable<T> documents)
        {
            DeleteAll();
            InsertMany(documents);
        }

        protected void DeleteAll()
        {
            LiteDbSampleContext.Execute(db =>
            {
                db.GetCollection<T>(_collectionName).DeleteAll();
            });
        }

        protected void DeleteMany(Func<T, bool> predicate)
        {
            if (predicate == null)
            {
                return;
            }

            var matches = Find(predicate);
            DeleteByIds(matches.Select(GetPrimaryKeyValue));
        }

        private void DeleteByIds(IEnumerable<object> ids)
        {
            LiteDbSampleContext.Execute(db =>
            {
                var col = db.GetCollection<T>(_collectionName);
                foreach (var id in ids)
                {
                    if (id == null)
                    {
                        continue;
                    }

                    col.Delete(BsonValue.Create(id));
                }
            });
        }

        private MemberInfo ResolvePrimaryKeyMember(string primaryKey)
        {
            var binding = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var property = typeof(T).GetProperty(primaryKey, binding);
            if (property != null)
            {
                return property;
            }

            var field = typeof(T).GetField(primaryKey, binding);
            if (field != null)
            {
                return field;
            }

            if (primaryKey.Equals("_id", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new InvalidOperationException($"Primary key '{primaryKey}' was not found on '{typeof(T).Name}'.");
        }

        private object GetPrimaryKeyValue(T entity)
        {
            if (entity == null || _primaryKeyMember == null)
            {
                return null;
            }

            return _primaryKeyMember switch
            {
                PropertyInfo propertyInfo => propertyInfo.GetValue(entity, null),
                FieldInfo fieldInfo => fieldInfo.GetValue(entity),
                _ => null
            };
        }
    }
}
