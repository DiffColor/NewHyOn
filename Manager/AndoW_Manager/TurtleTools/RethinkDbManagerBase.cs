using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TurtleTools;

namespace AndoW_Manager
{
    public abstract class RethinkDbManagerBase<T>
    {
        private readonly string _databaseName;
        private readonly string _tableName;
        private readonly string _primaryKeyName;
        private readonly MemberInfo _primaryKeyMember;

        protected RethinkDbManagerBase(string databaseName, string tableName, string primaryKeyName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("Database name is required.", nameof(databaseName));
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name is required.", nameof(tableName));
            }

            _primaryKeyName = string.IsNullOrWhiteSpace(primaryKeyName) ? "id" : primaryKeyName;

            RethinkDbConfigurator.EnsureConfigured();

            _databaseName = databaseName;
            _tableName = tableName;
            _primaryKeyMember = ResolvePrimaryKeyMember(_primaryKeyName);

            RethinkDbContext.EnsureTable(_databaseName, _tableName, _primaryKeyName);
        }

        protected List<T> LoadAllDocuments()
        {
            var table = RethinkDbContext.Table(_databaseName, _tableName);
            return RethinkDbContext.RunList<T>(table);
        }

        protected List<T> Find(Func<T, bool> predicate)
        {
            var documents = LoadAllDocuments();
            if (predicate == null)
            {
                return documents;
            }

            return documents.Where(predicate).ToList();
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

            var table = RethinkDbContext.Table(_databaseName, _tableName);
            return RethinkDbContext.RunSingleOrDefault<T>(table.Get(id));
        }

        protected void Upsert(T document)
        {
            if (document == null)
            {
                return;
            }

            var table = RethinkDbContext.Table(_databaseName, _tableName);
            var insert = table.Insert(document).OptArg("conflict", "replace");
            RethinkDbContext.Run(insert);
        }

        protected void InsertMany(IEnumerable<T> documents)
        {
            var list = documents?.ToList();
            if (list == null || list.Count == 0)
            {
                return;
            }

            var table = RethinkDbContext.Table(_databaseName, _tableName);
            var insert = table.Insert(list).OptArg("conflict", "replace");
            RethinkDbContext.Run(insert);
        }

        protected void ReplaceAllDocuments(IEnumerable<T> documents)
        {
            DeleteAll();
            InsertMany(documents);
        }

        protected void DeleteAll()
        {
            var table = RethinkDbContext.Table(_databaseName, _tableName);
            RethinkDbContext.Run(table.Delete());
        }

        protected void DeleteMany(Func<T, bool> predicate)
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

        protected void DeleteById(object id)
        {
            if (id == null)
            {
                return;
            }

            var table = RethinkDbContext.Table(_databaseName, _tableName);
            RethinkDbContext.Run(table.Get(id).Delete());
        }

        private void DeleteByIds(IEnumerable<object> ids)
        {
            foreach (var id in ids)
            {
                if (id == null)
                {
                    continue;
                }

                DeleteById(id);
            }
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
