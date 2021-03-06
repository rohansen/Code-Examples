﻿using SecureService.DataAccess.Cryptography;
using SecureService.DataAccess.Interfaces;
using SecureService.Domain;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace SecureService.DataAccess.ADO.SQLServer
{
    public class ADOUser : IDatabaseUserLogin<User>
    {
        private readonly string CONNECTION_STRING = ConfigurationManager.ConnectionStrings["MyDatabaseConnection"].ConnectionString;
        //If needed, you can sign up your logger to recieve events here (TODO: make custom eventhandler delegate)
        public event Action<string> LoginAttempt;
        public event Action<string> InvalidLoginAttempt;

        public ADOUser()
        {

        }

        //Add role when user signs up.. too lazy to do now
        public void Add(User entity)
        {
            //Idea: Maybe insert something to a junction table(booking or something), and make sure to implement the correct isolation level, to prevent overbooking, 
            //ADO and/or TransactionScope

            TransactionOptions opt = new TransactionOptions();
            //Could probably read if the user exists first to visualize that another transaction isolation level will be needed
            //BUt in this case the email is unique, and it isnt needed here
            opt.IsolationLevel = IsolationLevel.ReadCommitted;
            using (TransactionScope scope = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sql = "INSERT INTO [Users] (Email,Password,Salt) VALUES(@email,@password,@salt)";
                        cmd.CommandText = sql;
                        cmd.Parameters.AddWithValue("@email", entity.Email);
                        var newSalt = HashingManager.GenerateSalt();
                        var newHash = HashingManager.HashPassword(entity.Password, newSalt);
                        cmd.Parameters.AddWithValue("@password", newHash);
                        cmd.Parameters.AddWithValue("@salt", newSalt);
                        cmd.ExecuteNonQuery();
                    }

                    scope.Complete();
                }

            }
        }

        public IEnumerable<User> Find(string query)
        {
            //Look at Lucene? or implement fulltext search index (MSSQL native)
            throw new NotImplementedException();
        }
        public User Get(int id)
        {
            //stub, returns a hardcoded userobject.
            List<Role> userRoles = new List<Role> { new Role { Id = 1, Title = "Admin" } };
            return new User { Id = 1, Email = "roh@ucn.dk", Password = "1234", Roles = userRoles };
        }

        //Not used atm.
        public User Get(string email)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    string sql = "SELECT Users.Id as UserId,Email, Password, Roles.Id as RoleId," +
                                    "Title FROM Users" +
                                    "JOIN UserRoles ON Users.Id = UserRoles.UserId" +
                                    "JOIN Roles ON UserRoles.RoleId = Roles.Id" +
                                    "WHERE Email = @email";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("email", email);
                    var reader = cmd.ExecuteReader();
                    if (!reader.HasRows)
                    {
                        return null;
                    }
                    else
                    {
                        reader.Read();//advance pointer 1 row, and get the user information from the first row
                        var foundUser = new User();
                        foundUser.Id = reader.GetInt32(reader.GetOrdinal("UserId"));
                        foundUser.Email = reader.GetString(reader.GetOrdinal("Email"));
                        foundUser.Password = reader.GetString(reader.GetOrdinal("Password"));

                        while (reader.Read())//Continue advancing the pointer untill the end, and save the Role information
                        {
                            Role foundRole = new Role();
                            foundRole.Title = reader.GetString(reader.GetOrdinal("Title"));
                            foundRole.Id = reader.GetInt32(reader.GetOrdinal("RoleId"));
                            foundUser.Roles.Add(foundRole);
                        }
                        return foundUser;
                    }
                }
            }
        }

        //Logical requirement for this to function as intended, is that the users email is unique 
        //This method will only find users in a role.. the Add methods does not add role by default ... TODO:Fix
        public User Login(string username, string password)
        {
            //DbConnection is IDisposable
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    string sql = "SELECT Users.Id as UserId,Email, Password, Salt, Roles.Id as RoleId," +
                                    " Title FROM Users" +
                                    " JOIN UserRoles ON Users.Id = UserRoles.UserId" +
                                    " JOIN Roles ON UserRoles.RoleId = Roles.Id" +
                                    " WHERE Email = @email";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("email", username);
                    var reader = cmd.ExecuteReader();
                    if (!reader.HasRows)
                    {
                        if (InvalidLoginAttempt != null)
                        {
                            InvalidLoginAttempt("bla bla someone tried to log in with incorrect or not existing credentials, none found in DB");
                        }
                        return null;
                    }
                    else
                    {
                        if (LoginAttempt != null)
                        {
                            LoginAttempt(username + " log in credentials were found in the database");
                        }
                        reader.Read();//advance pointer 1 row, and get the user information from the first row
                        var foundUser = new User();
                        foundUser.Id = reader.GetInt32(reader.GetOrdinal("UserId"));
                        foundUser.Email = reader.GetString(reader.GetOrdinal("Email"));
                        foundUser.Password = reader.GetString(reader.GetOrdinal("Password"));
                        string currentSalt = reader.GetString(reader.GetOrdinal("Salt"));//TODO: Salt and Salted hash not in the test database!
                        string currentSaltedHash = reader.GetString(reader.GetOrdinal("Password"));
                        if (!HashingManager.CheckPassword(password, currentSalt, currentSaltedHash))
                        {
                            throw new Exception("Incorrect Credentials!");
                        }
                        //TODO: Forgot the first role!!!!
                        while (reader.Read())//Continue advancing the pointer untill the end, and save the Role information
                        {
                            Role foundRole = new Role();
                            foundRole.Title = reader.GetString(reader.GetOrdinal("Title"));
                            foundRole.Id = reader.GetInt32(reader.GetOrdinal("RoleId"));
                            foundUser.Roles.Add(foundRole);
                        }
                        return foundUser;
                    }
                }
            }
        }
        //SELECT * . maybe paging.
        public IEnumerable<User> GetAll()
        {
            throw new NotImplementedException();
        }
        //DELETE FROM .....
        public void Remove(User entity)
        {
            throw new NotImplementedException();
        }
    }
}
