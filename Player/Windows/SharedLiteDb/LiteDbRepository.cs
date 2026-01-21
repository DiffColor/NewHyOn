using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LiteDB;

namespace AndoW.LiteDb
{
    /// <summary>
    /// LiteDB 공통 CRUD를 담당하는 베이스 리포지토리.
    /// </summary>
    public abstract class LiteDbRepository<T>
    {
        private readonly string _collectionName;
        private readonly string _primaryKeyName;
        private readonly MemberInfo _primaryKeyMember;

        public LiteDbRepository(string collectionName, string primaryKeyName = "_id")
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name is required.", nameof(collectionName));
            }

            _collectionName = collectionName;
            _primaryKeyName = string.IsNullOrWhiteSpace(primaryKeyName) ? "_id" : primaryKeyName;
            _primaryKeyMember = ResolvePrimaryKeyMember(_primaryKeyName);

            LiteDbContext.EnsureCollection<T>(_collectionName);
        }

        public List<T> LoadAll()
        {
            return LiteDbContext.Execute(db =>
                db.GetCollection<T>(_collectionName).FindAll().ToList());
        }

        public List<T> Find(Func<T, bool> predicate)
        {
            var documents = LoadAll();
            return predicate == null ? documents : documents.Where(predicate).ToList();
        }

        public T FindOne(Func<T, bool> predicate)
        {
            return Find(predicate).FirstOrDefault();
        }

        public T FindById(object id)
        {
            if (id == null)
            {
                return default;
            }

            return LiteDbContext.Execute(db =>
                db.GetCollection<T>(_collectionName).FindById(new BsonValue(id)));
        }

        public void Upsert(T document)
        {
            if (document == null)
            {
                return;
            }

            LiteDbContext.Execute(db =>
            {
                db.GetCollection<T>(_collectionName).Upsert(document);
            });
        }

        public void UpsertMany(IEnumerable<T> documents)
        {
            var list = documents?.ToList();
            if (list == null || list.Count == 0)
            {
                return;
            }

            LiteDbContext.Execute(db =>
            {
                db.GetCollection<T>(_collectionName).Upsert(list);
            });
        }

        public void ReplaceAll(IEnumerable<T> documents)
        {
            LiteDbContext.Execute(db =>
            {
                var col = db.GetCollection<T>(_collectionName);
                col.DeleteAll();
                if (documents != null)
                {
                    col.Upsert(documents);
                }
            });
        }

        public void DeleteById(object id)
        {
            if (id == null)
            {
                return;
            }

            LiteDbContext.Execute(db =>
            {
                db.GetCollection<T>(_collectionName).Delete(new BsonValue(id));
            });
        }

        public void DeleteMany(Func<T, bool> predicate)
        {
            if (predicate == null)
            {
                return;
            }

            var matches = Find(predicate);
            if (matches.Count == 0)
            {
                return;
            }

            DeleteByIds(matches.Select(GetPrimaryKeyValue));
        }

        private void DeleteByIds(IEnumerable<object> ids)
        {
            LiteDbContext.Execute(db =>
            {
                var col = db.GetCollection<T>(_collectionName);
                foreach (var id in ids)
                {
                    if (id == null)
                    {
                        continue;
                    }

                    col.Delete(new BsonValue(id));
                }
            });
        }

        public MemberInfo ResolvePrimaryKeyMember(string primaryKey)
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

        public object GetPrimaryKeyValue(T entity)
        {
            if (entity == null || _primaryKeyMember == null)
            {
                return null;
            }

            var propertyInfo = _primaryKeyMember as PropertyInfo;
            if (propertyInfo != null)
            {
                return propertyInfo.GetValue(entity, null);
            }

            var fieldInfo = _primaryKeyMember as FieldInfo;
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(entity);
            }

            return null;
        }
    }
}
