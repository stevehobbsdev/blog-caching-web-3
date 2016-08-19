using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace CachingDemo.Web.Models.Data
{
	public interface IVehicleRepository
	{
		void ClearCache();
		IEnumerable<Vehicle> GetVehicles();
		void Insert(Vehicle vehicle);
		void Update(Vehicle vehicle);
		void SaveChanges();
	}

	public abstract class VehicleRepositoryBase : IVehicleRepository
	{
		public VehicleRepositoryBase()
			: this(new DefaultCacheProvider())
		{
		}

		public VehicleRepositoryBase(ICacheProvider cacheProvider)
		{
			this.Cache = cacheProvider;
		}

		/// <summary>
		/// Gets the key used to store vehicle data in the cache
		/// </summary>
		protected virtual string CacheKey
		{
			get { return "vehicles"; }
		}

		/// <summary>
		/// Gets the cache provider instance
		/// </summary>
		public ICacheProvider Cache { get; protected set; }

		/// <summary>
		/// Gets the vehicle data.
		/// </summary>
		public virtual IEnumerable<Vehicle> GetVehicles()
		{
			// First, check the cache
			var vehicleData = GetCachedData();

			// If it's not in the cache, we need to read it from the repository
			if (vehicleData == null)
			{
				// Get the data
				vehicleData = LoadData().ToDictionary(v => v.Id);

				if (vehicleData.Any())
				{
					// Put this data into the cache for 30 minutes
					Cache.Set(CacheKey, vehicleData, 30);
				}
			}

			return vehicleData.Values;
		}

		/// <summary>
		/// Loads the data from the data store
		/// </summary>
		protected abstract IEnumerable<Vehicle> LoadData();

		/// <summary>
		/// Inserts a new vehicle
		/// </summary>
		public abstract void Insert(Vehicle vehicle);

		/// <summary>
		/// Updates a vehicle
		/// </summary>
		public abstract void Update(Vehicle vehicle);

		/// <summary>
		/// Saves the data changes.
		/// </summary>
		public abstract void SaveChanges();

		/// <summary>
		/// Clears the data from the cache.
		/// </summary>
		public void ClearCache()
		{
			Cache.Invalidate(this.CacheKey);
		}

		protected Dictionary<Guid, Vehicle> GetCachedData()
		{
			var cacheData = Cache.Get(CacheKey) as Dictionary<Guid, Vehicle>;
			return cacheData;
		}
	}

	public class VehicleRepository : VehicleRepositoryBase
	{
		protected CachingDemoEntities DataContext { get; private set; }

		public VehicleRepository()
			: this(new DefaultCacheProvider())
		{
		}

		public VehicleRepository(ICacheProvider cacheProvider)
			: base(cacheProvider)
		{
			this.DataContext = new CachingDemoEntities();
		}

		/// <summary>
		/// Load the data from the data source
		/// </summary>
		protected override IEnumerable<Vehicle> LoadData()
		{
			return DataContext.Vehicles.OrderBy(v => v.Name).ToList();
		}

		/// <summary>
		/// Updates a vehicle
		/// </summary>
		public override void Update(Vehicle vehicle)
		{
			if (vehicle.EntityState == EntityState.Detached)
			{
				DataContext.AttachTo("Vehicles", vehicle);
			}
			DataContext.ObjectStateManager.ChangeObjectState(vehicle, EntityState.Modified);
		}

		/// <summary>
		/// Inserts a vehicle
		/// </summary>
		public override void Insert(Vehicle vehicle)
		{
			DataContext.AddToVehicles(vehicle);
		}

		/// <summary>
		/// Saves data changes to the data store
		/// </summary>
		public override void SaveChanges()
		{
			// Update or add new/existing entities from the changeset
			var changeset = DataContext.ObjectStateManager.GetObjectStateEntries(EntityState.Added | EntityState.Modified);

			DataContext.SaveChanges();

			var cacheData = GetCachedData();

			if (cacheData != null)
			{
				foreach (var item in changeset)
				{
					var vehicle = item.Entity as Vehicle;
					cacheData[vehicle.Id] = vehicle;
				}
			}
		}
	}
}