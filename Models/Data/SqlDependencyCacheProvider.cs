using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;

namespace CachingDemo.Web.Models.Data
{
	public class SqlDependancyCacheProvider : ICacheProvider
	{
		private Cache _cache;

		public string DbEntryName { get; set; }

		public string TableName { get; set; }

		public SqlDependancyCacheProvider(string dbEntryName, string tableName)
		{
			_cache = System.Web.HttpRuntime.Cache;
			DbEntryName = dbEntryName;
			TableName = tableName;
		}

		public object Get(string key)
		{
			return _cache[key];
		}

		public void Set(string key, object data, int cacheTime)
		{
			SqlCacheDependency dep = new SqlCacheDependency(DbEntryName, TableName);
			_cache.Add(key, data, dep, Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(cacheTime), System.Web.Caching.CacheItemPriority.Normal, null);
		}

		public bool IsSet(string key)
		{
			return (_cache[key] != null);
		}

		public void Invalidate(string key)
		{
			_cache.Remove(key);
		}
	}
}