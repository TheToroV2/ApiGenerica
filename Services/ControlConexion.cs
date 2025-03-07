#nullable enable // Habilita las características de referencia nula en C#, permitiendo anotaciones y advertencias relacionadas con posibles valores nulos.
using System; // Importa el espacio de nombres System, que contiene tipos fundamentales como Exception, Console, etc.
using System.Collections.Generic; // Importa el espacio de nombres para colecciones genéricas como Dictionary.
using System.Data; // Importa el espacio de nombres para acceder a clases relacionadas con bases de datos.
using System.Data.Common; // Importa el espacio de nombres que define la clase base para proveedores de datos.
using Microsoft.Data.SqlClient; // Importa el espacio de nombres necesario para trabajar con SQL Server y LocalDB.
using System.IO; // Importa el espacio de nombres para manejar archivos y directorios.
using Microsoft.AspNetCore.Hosting; // Importa el espacio de nombres para trabajar con el entorno de hospedaje de la aplicación.
using Microsoft.Extensions.Configuration; // Importa el espacio de nombres para trabajar con la configuración de la aplicación.

namespace csharpapi.Services
{
    public class ControlConexion
    {

private readonly IWebHostEnvironment _entorno; // Define una variable para almacenar el entorno de hospedaje web.
private readonly IConfiguration _configuracion; // Define una variable para almacenar la configuración de la aplicación.
private IDbConnection? _conexionBd; // Define una variable para almacenar la conexión a la base de datos.

// Constructor que inicializa el entorno de hospedaje web y la configuración de la aplicación.
public ControlConexion(IWebHostEnvironment entorno, IConfiguration configuracion)
{
    _entorno = entorno ?? throw new ArgumentNullException(nameof(entorno)); // Inicializa _entorno y lanza una excepción si es null.
    _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion)); // Inicializa _configuracion y lanza una excepción si es null.
    _conexionBd = null; // Inicializa la conexión a la base de datos como null.
}


// Método para abrir la base de datos, compatible con LocalDB y SQL Server.
public void AbrirBd()
{
    try
    {
        string proveedor = _configuracion["DatabaseProvider"] ?? throw new InvalidOperationException("Proveedor de base de datos no configurado.");
        string? cadenaConexion = _configuracion.GetConnectionString(proveedor);

        if (string.IsNullOrEmpty(cadenaConexion))
            throw new InvalidOperationException("La cadena de conexión es nula o vacía.");

        Console.WriteLine($"Intentando abrir conexión con el proveedor: {proveedor}");
        Console.WriteLine($"Cadena de conexión: {cadenaConexion}");

        switch (proveedor)
        {
            case "LocalDb":
                string rutaAppData = Path.Combine(_entorno.ContentRootPath, "App_Data");
                AppDomain.CurrentDomain.SetData("DataDirectory", rutaAppData);
                _conexionBd = new SqlConnection(cadenaConexion);
                break;
            case "SqlServer":
                _conexionBd = new SqlConnection(cadenaConexion);
                break;
            default:
                throw new InvalidOperationException("Proveedor de base de datos no soportado. Solo se admiten LocalDb y SqlServer.");
        }

        _conexionBd.Open();
        Console.WriteLine("Conexión a la base de datos abierta exitosamente.");
    }
    catch (SqlException ex)
    {
        Console.WriteLine($"Ocurrió una SqlException: {ex.Message}");
        Console.WriteLine($"Número de Error: {ex.Number}");
        Console.WriteLine($"Estado de Error: {ex.State}");
        Console.WriteLine($"Clase de Error: {ex.Class}");
        throw new InvalidOperationException("Error al abrir la conexión a la base de datos debido a un error SQL.", ex);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ocurrió una excepción: {ex.Message}");
        throw new InvalidOperationException("Error al abrir la conexión a la base de datos.", ex);
    }
}

// Método específico para abrir una base de datos LocalDB.
public void AbrirBdLocalDB(string archivoBd)
{
    try
    {
        // Verifica si el nombre del archivo termina en .mdf, si no, lo agrega.
        string nombreArchivoBd = archivoBd.EndsWith(".mdf") ? archivoBd : archivoBd + ".mdf";
        
        // Define la ruta completa al archivo de base de datos en la carpeta App_Data.
        string rutaAppData = Path.Combine(_entorno.ContentRootPath, "App_Data");
        string rutaArchivo = Path.Combine(rutaAppData, nombreArchivoBd);

        // Crea la cadena de conexión para LocalDB con AttachDbFilename.
        string cadenaConexion = $@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename={rutaArchivo};Integrated Security=True";
        
        // Abre la conexión a la base de datos LocalDB.
        _conexionBd = new SqlConnection(cadenaConexion);
        _conexionBd.Open();
    }
    catch (Exception ex)
    {
        // Lanza una excepción personalizada si la conexión falla.
        throw new InvalidOperationException("Error al abrir la conexión a LocalDB.", ex);
    }
}
        

// Método para cerrar la conexión a la base de datos.
public void CerrarBd()
{
    try
    {
        // Verifica si la conexión está abierta y luego la cierra.
        if (_conexionBd != null && _conexionBd.State == ConnectionState.Open)
        {
            _conexionBd.Close();
        }
    }
    catch (Exception ex)
    {
        // Lanza una excepción personalizada si la operación de cierre falla.
        throw new InvalidOperationException("Error al cerrar la conexión a la base de datos.", ex);
    }
}

// Método para ejecutar un comando SQL y devolver el número de filas afectadas.
public int EjecutarComandoSql(string consultaSql, DbParameter[] parametros)
{
    try
    {
        // Verifica si la conexión está abierta antes de ejecutar el comando.
        if (_conexionBd == null || _conexionBd.State != ConnectionState.Open)
            throw new InvalidOperationException("La conexión a la base de datos no está abierta.");

        // Crea y configura un comando SQL.
        using (var comando = _conexionBd.CreateCommand())
        {
            comando.CommandText = consultaSql; // Asigna la consulta SQL al comando.
            foreach (var parametro in parametros)
            {
                // Agrega cada parámetro al comando.
                Console.WriteLine($"Agregando parámetro: {parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}");
                comando.Parameters.Add(parametro);
            }
            // Ejecuta el comando y devuelve el número de filas afectadas.
            int filasAfectadas = comando.ExecuteNonQuery();
            return filasAfectadas;
        }
    }
    catch (Exception ex)
    {
        // Lanza una excepción personalizada si la ejecución del comando falla.
        Console.WriteLine($"Ocurrió una excepción: {ex.Message}");
        throw new InvalidOperationException("Error al ejecutar el comando SQL.", ex);
    }
}

// Método para ejecutar una consulta SQL y devolver un DataTable con los resultados.
public DataTable EjecutarConsultaSql(string consultaSql, DbParameter[]? parametros)
{
    // Verifica si la conexión está abierta antes de ejecutar la consulta.
    if (_conexionBd == null || _conexionBd.State != ConnectionState.Open)
        throw new InvalidOperationException("La conexión a la base de datos no está abierta.");

    try
    {
        // Crea y configura un comando SQL.
        using (var comando = _conexionBd.CreateCommand())
        {
            comando.CommandText = consultaSql; // Asigna la consulta SQL al comando.
            if (parametros != null)
            {
                // Agrega los parámetros al comando si los hay.
                foreach (var param in parametros)
                {
                    Console.WriteLine($"Agregando parámetro: {param.ParameterName} = {param.Value}, DbType: {param.DbType}");
                    comando.Parameters.Add(param);
                }
            }

            // Crea un DataSet para almacenar los resultados de la consulta.
            var resultado = new DataSet();
            var adaptador = new SqlDataAdapter((SqlCommand)comando); // Crea un adaptador de datos para SQL Server.

            Console.WriteLine($"Ejecutando comando: {comando.CommandText}");
            adaptador.Fill(resultado); // Llena el DataSet con los resultados de la consulta.
            Console.WriteLine("DataSet lleno");

            // Verifica si el DataSet tiene al menos una tabla de resultados.
            if (resultado.Tables.Count == 0)
            {
                Console.WriteLine("No se devolvieron tablas en el DataSet");
                throw new Exception("No se devolvieron tablas en el DataSet");
            }

            Console.WriteLine($"Número de tablas en el DataSet: {resultado.Tables.Count}");
            Console.WriteLine($"Número de filas en la primera tabla: {resultado.Tables[0].Rows.Count}");

            return resultado.Tables[0]; // Retorna la primera tabla del DataSet.
        }
    }
    catch (Exception ex)
    {
        // Lanza una excepción personalizada si la consulta falla.
        Console.WriteLine($"Ocurrió una excepción: {ex.Message}");
        throw new Exception($"Error al ejecutar la consulta SQL. Error: {ex.Message}", ex);
    }
}

// Método para crear un parámetro de consulta SQL.
public DbParameter CrearParametro(string nombre, object? valor)
{
    try
    {
        // Obtiene el proveedor de base de datos desde la configuración, lanza una excepción si no está configurado.
        string proveedor = _configuracion["DatabaseProvider"] ?? throw new InvalidOperationException("Proveedor de base de datos no configurado.");
        
        // Crea un parámetro adecuado según el proveedor de base de datos.
        return proveedor switch
        {
            "SqlServer" => new SqlParameter(nombre, valor ?? DBNull.Value), // Crea un parámetro para SQL Server.
            "LocalDb" => new SqlParameter(nombre, valor ?? DBNull.Value), // Crea un parámetro para LocalDB.
            _ => throw new InvalidOperationException("Proveedor de base de datos no soportado. Solo se admiten LocalDb y SqlServer."),
        };
    }
    catch (Exception ex)
    {
        // Lanza una excepción personalizada si la creación del parámetro falla.
        throw new InvalidOperationException("Error al crear el parámetro.", ex);
    }
}

// Método para obtener la conexión actual a la base de datos.
public IDbConnection? ObtenerConexion()
{
    return _conexionBd; // Devuelve la conexión actual a la base de datos.
}
        // Método para ejecutar un procedimiento almacenado y devolver un DataTable con los resultados.
        public DataTable EjecutarProcedimientoAlmacenado(string nombreProcedimiento, DbParameter[]? parametros)
        {
            if (_conexionBd == null || _conexionBd.State != ConnectionState.Open)
                throw new InvalidOperationException("La conexión no está abierta.");

            try
            {
                using (var comando = _conexionBd.CreateCommand())
                {
                    comando.CommandText = nombreProcedimiento;
                    comando.CommandType = CommandType.StoredProcedure;
                    
                    if (parametros != null)
                    {
                        foreach (var param in parametros)
                        {
                            comando.Parameters.Add(param);
                        }
                    }

                    var resultado = new DataSet();
                    var adaptador = new SqlDataAdapter((SqlCommand)comando);
                    adaptador.Fill(resultado);

                    return resultado.Tables[0];
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar el procedimiento almacenado: {ex.Message}", ex);
            }
        }

        // Método para ejecutar una función SQL y devolver un resultado escalar.
        public object? EjecutarFuncion(string nombreFuncion, DbParameter[]? parametros)
        {
            if (_conexionBd == null || _conexionBd.State != ConnectionState.Open)
                throw new InvalidOperationException("La conexión no está abierta.");

            try
            {
                using (var comando = _conexionBd.CreateCommand())
                {
                    comando.CommandText = nombreFuncion;
                    comando.CommandType = CommandType.Text;
                    
                    if (parametros != null)
                    {
                        foreach (var param in parametros)
                        {
                            comando.Parameters.Add(param);
                        }
                    }

                    return comando.ExecuteScalar(); // Devuelve un solo valor de la función SQL.
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar la función SQL: {ex.Message}", ex);
            }
        }

    }
}

/*
IConfiguration
IConfiguration es una interfaz en ASP.NET Core que se utiliza para acceder a la configuración 
de la aplicación. La configuración puede provenir de diferentes fuentes, 
como archivos de configuración (por ejemplo, appsettings.json), 
variables de entorno, argumentos de la línea de comandos, y más.

IDbConnection
IDbConnection es una interfaz en ADO.NET que define los métodos y 
propiedades que debe implementar cualquier clase que represente una conexión a 
una base de datos. Esta interfaz es parte del espacio de nombres System.Data.

Características y uso:
Propósito: Proporciona una manera estandarizada de manejar conexiones a bases de datos, 
independientemente del tipo específico de base de datos 
(por ejemplo, SQL Server, MySQL, Oracle, etc.).

`TrustServerCertificate=True` en tu cadena de conexión, es una solución temporal y no se recomienda 
para entornos de producción. En el futuro, se debe considerar implementar una solución más segura, 
como instalar un certificado válido en tu instancia de SQL Server.
*/
