namespace WebApplication1.DAL;

using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.Data;

public abstract class BaseDAL
{
    private readonly string _connectionString;

    public BaseDAL(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleDb")
                            ?? throw new Exception("Brak DefaultConnection w appsettings.json");
    }

    protected OracleConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }
}