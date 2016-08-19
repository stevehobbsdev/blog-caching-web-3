using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System.Transactions;
using System.Runtime.Caching;
using System.Web.Caching;
using System.Diagnostics;

namespace CachingDemo.Web.Models.Data
{
	public class SqlVehicleRepository : VehicleRepositoryBase
	{
		#region Query Constants

		private const string VEHICLE_SELECT = @"Select * From Vehicle";
		private const string VEHICLE_INSERT = @"Insert Into Vehicle (Id, Name, Price) Values (@Id, @Name, @Price)";
		private const string VEHICLE_UPDATE = @"Update Vehicle Set Name=@Name, Price=@Price Where Id=@Id";

		#endregion

		#region Unit of Work

		private HashSet<Vehicle> _inserts;
		private HashSet<Vehicle> _updates;

		#endregion

		public SqlVehicleRepository()
			: this(new SqlDependancyCacheProvider("CachingDemo", "Vehicle"))
		{
		}

		public SqlVehicleRepository(ICacheProvider cacheProvider)
			:base(cacheProvider)
		{
			_inserts = new HashSet<Vehicle>();
			_updates = new HashSet<Vehicle>();
		}

		/// <summary>
		/// Creates the SqlConnection instance
		/// </summary>
		private SqlConnection CreateConnection()
		{
			return new SqlConnection(ConfigurationManager.ConnectionStrings["DemoConnectionString"].ConnectionString);
		}

		protected override IEnumerable<Vehicle> LoadData()
		{
			var connection = CreateConnection();
			var command = new SqlCommand()
			{
				Connection = connection,
				CommandType = System.Data.CommandType.Text,
				CommandText = VEHICLE_SELECT
			};

			List<Vehicle> vehicles = new List<Vehicle>();

			using (connection)
			{
				connection.Open();

				var reader = command.ExecuteReader();

				if (reader.HasRows)
				{
					while (reader.Read())
					{
						var vehicle = new Vehicle()
						{
							Id = (Guid)reader["Id"],
							Name = (string)reader["Name"],
							Price = (decimal)reader["Price"]
						};

						vehicles.Add(vehicle);
					}
				}
			}

			return vehicles;
		}

		public override void Insert(Vehicle vehicle)
		{
			if (!_inserts.Contains(vehicle))
				_inserts.Add(vehicle);
		}

		public override void Update(Vehicle vehicle)
		{
			if (!_updates.Contains(vehicle))
				_updates.Add(vehicle);
		}

		public override void SaveChanges()
		{
			var cached = GetCachedData();
			List<Vehicle> itemsToRecache = new List<Vehicle>();

			using (var connection = CreateConnection())
			{
				connection.Open();

				using (TransactionScope scope = new TransactionScope())
				{
					// Process inserts
					foreach (var vehicle in _inserts)
					{
						var command = new SqlCommand() { Connection = connection, CommandType = System.Data.CommandType.Text };

						command.CommandText = VEHICLE_INSERT;
						command.Parameters.Add(CreateParameter("Id", SqlDbType.UniqueIdentifier, vehicle.Id));
						command.Parameters.Add(CreateParameter("Name", SqlDbType.VarChar, vehicle.Name));
						command.Parameters.Add(CreateParameter("Price", SqlDbType.Decimal, vehicle.Price));

						command.ExecuteNonQuery();

						// Insert this vehicle into our cache
						itemsToRecache.Add(vehicle);
					}

					// Process updates:
					foreach (var vehicle in _updates)
					{
						var command = new SqlCommand() { Connection = connection, CommandType = CommandType.Text };

						command.CommandText = VEHICLE_UPDATE;
						command.Parameters.Add(CreateParameter("Id", SqlDbType.UniqueIdentifier, vehicle.Id));
						command.Parameters.Add(CreateParameter("Name", SqlDbType.VarChar, vehicle.Name));
						command.Parameters.Add(CreateParameter("Price", SqlDbType.Decimal, vehicle.Price));

						command.ExecuteNonQuery();

						itemsToRecache.Add(vehicle);
					}

					scope.Complete();
				}
			}

			itemsToRecache.ForEach(item =>
			{
				cached[item.Id] = item;
			});

			_inserts.Clear();
			_updates.Clear();
		}

		private SqlParameter CreateParameter(string name, SqlDbType type, object value)
		{
			var param = new SqlParameter(name, type);
			param.Value = value;
			return param;
		}
	}
}