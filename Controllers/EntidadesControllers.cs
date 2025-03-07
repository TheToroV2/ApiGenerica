#nullable enable // Habilita las características de referencia nula en C#, permitiendo anotaciones y advertencias relacionadas con posibles valores nulos.
using System; // Importa el espacio de nombres que contiene tipos fundamentales como Exception, Console, etc.
using System.Collections.Generic; // Importa el espacio de nombres para colecciones genéricas como Dictionary.
using System.Data; // Importa el espacio de nombres para clases relacionadas con bases de datos.
using System.Data.Common; // Importa el espacio de nombres que define la clase base para proveedores de datos.
using Microsoft.AspNetCore.Authorization; // Importa el espacio de nombres para el control de autorización en ASP.NET Core.
using Microsoft.AspNetCore.Mvc; // Importa el espacio de nombres para la creación de controladores en ASP.NET Core.
using Microsoft.Extensions.Configuration; // Importa el espacio de nombres para acceder a la configuración de la aplicación.
using Microsoft.Data.SqlClient; // Importa el espacio de nombres necesario para trabajar con SQL Server y LocalDB.
using System.Linq; // Importa el espacio de nombres para operaciones de consulta con LINQ.
using System.Text.Json; // Importa el espacio de nombres para manejar JSON.
//using csharpapi.Models; // Importa los modelos del proyecto.
using csharpapi.Services; // Importa los servicios del proyecto.
using BCrypt.Net; // Importa el espacio de nombres para trabajar con BCrypt para hashing de contraseñas.

namespace ProyectoBackendCsharp.Controllers
{
    // Define la ruta base de la API usando variables dinámicas para mayor flexibilidad
    [Route("api/{nombreProyecto}/{nombreTabla}")]
    [ApiController] // Marca la clase como un controlador de API en ASP.NET Core.
    [Authorize] // Aplica autorización para que solo usuarios autenticados puedan acceder a estos endpoints.
    public class EntidadesController : ControllerBase
    {
        private readonly ControlConexion controlConexion; // Servicio para manejar la conexión a la base de datos.
        private readonly IConfiguration _configuration; // Configuración de la aplicación para obtener valores de appsettings.json.
        
        // Constructor que inyecta los servicios necesarios
        public EntidadesController(ControlConexion controlConexion, IConfiguration configuration)
        {
            this.controlConexion = controlConexion ?? throw new ArgumentNullException(nameof(controlConexion));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Obtiene todos los registros de una tabla específica en la base de datos.
        /// </summary>
        /// <param name="nombreProyecto">Nombre del proyecto al que pertenece la tabla.</param>
        /// <param name="nombreTabla">Nombre de la tabla a consultar.</param>
        /// <returns>Lista de registros en formato JSON si la consulta es exitosa, o un código de error en caso de fallo.</returns>
        /// <response code="200">Devuelve la lista de registros de la tabla.</response>
        /// <response code="400">El nombre de la tabla es inválido o está vacío.</response>
        /// <response code="404">La tabla no existe en la base de datos.</response>
        /// <response code="409">Error de restricción en la base de datos (clave foránea o clave duplicada).</response>
        /// <response code="500">Error interno del servidor.</response>
        [AllowAnonymous] // Permite que cualquier usuario acceda a este endpoint, sin necesidad de autenticación.
        [HttpGet] // Define que este método responde a solicitudes HTTP GET.
        public IActionResult Listar(string nombreProyecto, string nombreTabla) // Método para listar los registros de una tabla específica.
        {
            // Verifica si el nombre de la tabla es nulo o vacío
            if (string.IsNullOrWhiteSpace(nombreTabla)) 
                return BadRequest("El nombre de la tabla no puede estar vacío.");

            try
            {
                var listaFilas = new List<Dictionary<string, object?>>(); // Lista para almacenar las filas obtenidas de la base de datos.
                string comandoSQL = $"SELECT * FROM {nombreTabla}"; // Consulta SQL para obtener todos los registros de la tabla.

                controlConexion.AbrirBd(); // Abre la conexión con la base de datos.
                var tablaResultados = controlConexion.EjecutarConsultaSql(comandoSQL, null); // Ejecuta la consulta y obtiene los datos en un DataTable.
                controlConexion.CerrarBd(); // Cierra la conexión para liberar recursos.

                // Recorre cada fila del resultado y la convierte en un diccionario clave-valor.
                foreach (DataRow fila in tablaResultados.Rows)
                {
                    var propiedadesFila = fila.Table.Columns.Cast<DataColumn>()
                        .ToDictionary(columna => columna.ColumnName, 
                                    columna => fila[columna] == DBNull.Value ? null : fila[columna]);
                    listaFilas.Add(propiedadesFila); // Agrega la fila convertida a la lista.
                }

                return Ok(listaFilas); // Devuelve la lista de registros en formato JSON con código de estado 200 (OK).
            }
            catch (Exception ex)
            {
                int codigoError;
                string mensajeError;

                if (ex is SqlException sqlEx)
                {
                    // Mapea códigos de error SQL a códigos HTTP
                    codigoError = sqlEx.Number switch
                    {
                        208 => 404, // Tabla no encontrada
                        547 => 409, // Violación de restricción (clave foránea)
                        2627 => 409, // Clave única duplicada
                        _ => 500 // Otros errores desconocidos
                    };
                    mensajeError = $"Error ({codigoError}): {sqlEx.Message}";
                }
                else
                {
                    codigoError = 500; // Error interno del servidor.
                    mensajeError = $"Error interno del servidor: {ex.Message}";
                }
                return StatusCode(codigoError, mensajeError); // Devuelve un mensaje de error con el código correspondiente.
            }
        }


        /// <summary>
        /// Obtiene un registro específico de una tabla, basado en una clave y su valor.
        /// </summary>
        /// <param name="nombreProyecto">Nombre del proyecto al que pertenece la tabla.</param>
        /// <param name="nombreTabla">Nombre de la tabla en la base de datos.</param>
        /// <param name="nombreClave">Nombre de la columna clave utilizada para la búsqueda.</param>
        /// <param name="valor">Valor de la clave para filtrar el registro.</param>
        /// <returns>Un registro en formato JSON si se encuentra, o un código de error en caso de fallo.</returns>
        /// <response code="200">Devuelve el registro encontrado en la tabla.</response>
        /// <response code="400">Uno o más parámetros proporcionados son inválidos o están vacíos.</response>
        /// <response code="404">No se encontró el registro con el valor especificado.</response>
        /// <response code="500">Error interno del servidor.</response>
        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpGet("{nombreClave}/{valor}")] // Define una ruta HTTP GET con parámetros adicionales.
        public IActionResult ObtenerPorClave(string nombreProyecto, string nombreTabla, string nombreClave, string valor) // Método que obtiene una fila específica basada en una clave.
        {
            if (string.IsNullOrWhiteSpace(nombreTabla) || string.IsNullOrWhiteSpace(nombreClave) || string.IsNullOrWhiteSpace(valor)) // Verifica si alguno de los parámetros está vacío.
            {
                return BadRequest("El nombre de la tabla, el nombre de la clave y el valor no pueden estar vacíos."); // Retorna una respuesta de error si algún parámetro está vacío.
            }

            controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
            try
            {
                string proveedor = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("Proveedor de base de datos no configurado."); // Obtiene el proveedor de base de datos desde la configuración.

                string consultaSQL;
                DbParameter[] parametros;

                // Define la consulta SQL y los parámetros para SQL Server y LocalDB.
                consultaSQL = "SELECT data_type FROM information_schema.columns WHERE table_name = @nombreTabla AND column_name = @nombreColumna";
                parametros = new DbParameter[]
                {
                    CrearParametro("@nombreTabla", nombreTabla),
                    CrearParametro("@nombreColumna", nombreClave)
                };

                Console.WriteLine($"Ejecutando consulta SQL: {consultaSQL} con parámetros: nombreTabla={nombreTabla}, nombreColumna={nombreClave}");

                var resultadoTipoDato = controlConexion.EjecutarConsultaSql(consultaSQL, parametros); // Ejecuta la consulta SQL para determinar el tipo de dato de la clave.

                if (resultadoTipoDato == null || resultadoTipoDato.Rows.Count == 0 || resultadoTipoDato.Rows[0]["data_type"] == DBNull.Value) // Verifica si se obtuvo un resultado válido.
                {
                    return NotFound("No se pudo determinar el tipo de dato."); // Retorna una respuesta de error si no se pudo determinar el tipo de dato.
                }

                // Obtiene el tipo de dato de la columna en la base de datos.
                string tipoDato = resultadoTipoDato.Rows[0]["data_type"]?.ToString() ?? "";

                Console.WriteLine($"Tipo de dato detectado para la columna {nombreClave}: {tipoDato}");

                if (string.IsNullOrEmpty(tipoDato)) // Verifica si el tipo de dato es válido.
                {
                    return NotFound("No se pudo determinar el tipo de dato."); // Retorna una respuesta de error si el tipo de dato es inválido.
                }

                object valorConvertido;
                string comandoSQL;

                // Determina cómo tratar el valor y la consulta SQL según el tipo de dato.
                switch (tipoDato.ToLower())
                {
                    case "int":
                    case "bigint":
                    case "smallint":
                    case "tinyint":
                        if (int.TryParse(valor, out int valorEntero))
                        {
                            valorConvertido = valorEntero;
                            comandoSQL = $"SELECT * FROM {nombreTabla} WHERE {nombreClave} = @Valor";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos entero.");
                        }
                        break;
                    case "decimal":
                    case "numeric":
                    case "money":
                    case "smallmoney":
                        if (decimal.TryParse(valor, out decimal valorDecimal))
                        {
                            valorConvertido = valorDecimal;
                            comandoSQL = $"SELECT * FROM {nombreTabla} WHERE {nombreClave} = @Valor";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos decimal.");
                        }
                        break;
                    case "bit":
                        if (bool.TryParse(valor, out bool valorBooleano))
                        {
                            valorConvertido = valorBooleano;
                            comandoSQL = $"SELECT * FROM {nombreTabla} WHERE {nombreClave} = @Valor";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos booleano.");
                        }
                        break;
                    case "float":
                    case "real":
                        if (double.TryParse(valor, out double valorDoble))
                        {
                            valorConvertido = valorDoble;
                            comandoSQL = $"SELECT * FROM {nombreTabla} WHERE {nombreClave} = @Valor";
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos flotante.");
                        }
                        break;
                    case "nvarchar":
                    case "varchar":
                    case "nchar":
                    case "char":
                    case "text":
                        valorConvertido = valor;
                        comandoSQL = $"SELECT * FROM {nombreTabla} WHERE {nombreClave} = @Valor";
                        break;
                    case "date":
                    case "datetime":
                    case "datetime2":
                    case "smalldatetime":
                        if (DateTime.TryParse(valor, out DateTime valorFecha))
                        {
                            comandoSQL = $"SELECT * FROM {nombreTabla} WHERE CAST({nombreClave} AS DATE) = @Valor";
                            valorConvertido = valorFecha.Date;
                        }
                        else
                        {
                            return BadRequest("El valor proporcionado no es válido para el tipo de datos fecha.");
                        }
                        break;
                    default:
                        return BadRequest($"Tipo de dato no soportado: {tipoDato}"); // Retorna un error si el tipo de dato no es soportado.
                }

                var parametro = CrearParametro("@Valor", valorConvertido); // Crea el parámetro para la consulta SQL.

                Console.WriteLine($"Ejecutando consulta SQL: {comandoSQL} con parámetro: {parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}");

                var resultado = controlConexion.EjecutarConsultaSql(comandoSQL, new DbParameter[] { parametro }); // Ejecuta la consulta SQL con el parámetro.

                Console.WriteLine($"DataSet completado para la consulta: {comandoSQL}");

                if (resultado.Rows.Count > 0) // Verifica si hay filas en el resultado.
                {
                    var lista = new List<Dictionary<string, object?>>();
                    foreach (DataRow fila in resultado.Rows)
                    {
                        var propiedades = resultado.Columns.Cast<DataColumn>()
                                        .ToDictionary(columna => columna.ColumnName, columna => fila[columna] == DBNull.Value ? null : fila[columna]);
                        lista.Add(propiedades);
                    }

                    return Ok(lista); // Retorna las filas encontradas en formato JSON.
                }

                return NotFound(); // Retorna un error 404 si no se encontraron filas.
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                Console.WriteLine($"Ocurrió una excepción: {ex.Message}");
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción.
            }
            finally
            {
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.
            }
        }



        // Método para crear un parámetro de consulta SQL basado en el proveedor de base de datos.
        // Este método ayuda a evitar inyecciones SQL y manejar valores nulos de manera segura.
        //[ApiExplorerSettings(IgnoreApi = true)] // Indica que este método no debe ser documentado en Swagger.
        private DbParameter CrearParametro(string nombre, object? valor)
        {
            /*
            📌 Explicación de los parámetros:
            - `nombre` (string): Representa el nombre del parámetro en la consulta SQL. 
            Ejemplo: "@id", "@nombre", "@fecha".
            
            - `valor` (object?): Es el valor que se asignará al parámetro en la consulta SQL.
            Puede ser de cualquier tipo de dato: int, string, decimal, DateTime, etc.
            El signo `?` indica que el parámetro puede ser nulo.

            ⚠️ Manejo de valores nulos:
            - Si `valor` es `null`, el operador `??` asigna `DBNull.Value` automáticamente.
            - `DBNull.Value` representa un valor nulo en la base de datos de SQL Server.
            Esto es necesario porque en .NET `null` no es lo mismo que `DBNull.Value`.

            🛠 Ejemplo de uso en una consulta:
            - Suponiendo que tenemos:
                `int idUsuario = 5;`
            - Se llamaría así:
                `var parametro = CrearParametro("@id", idUsuario);`
            - Esto generaría:
                `SqlParameter("@id", 5);`
            
            📌 Ejemplo de un valor nulo:
            - `var parametro = CrearParametro("@email", null);`
            - Esto generaría:
                `SqlParameter("@email", DBNull.Value);`
            */

            return new SqlParameter(nombre, valor ?? DBNull.Value); // Crea un parámetro SQL de forma segura.
        }


        /// <summary>
        /// Crea un nuevo registro en la tabla especificada con los datos proporcionados.
        /// </summary>
        /// <param name="nombreProyecto">Nombre del proyecto al que pertenece la tabla.</param>
        /// <param name="nombreTabla">Nombre de la tabla en la base de datos.</param>
        /// <param name="datosEntidad">Diccionario con los datos de la entidad a insertar.</param>
        /// <returns>Un mensaje de éxito si la inserción es correcta, o un código de error en caso de fallo.</returns>
        /// <response code="200">Devuelve un mensaje indicando que la entidad fue creada exitosamente.</response>
        /// <response code="400">El nombre de la tabla o los datos de la entidad están vacíos.</response>
        /// <response code="500">Error interno del servidor.</response>
        [AllowAnonymous] // Permite que cualquier usuario acceda a este método sin necesidad de autenticación.
        [HttpPost] // Indica que este método maneja solicitudes HTTP POST.
        public IActionResult Crear(string nombreProyecto, string nombreTabla, [FromBody] Dictionary<string, object?> datosEntidad)
        {
            // Verifica si el nombre de la tabla es nulo o vacío, o si los datos a insertar están vacíos.
            if (string.IsNullOrWhiteSpace(nombreTabla) || datosEntidad == null || !datosEntidad.Any())
                return BadRequest("El nombre de la tabla y los datos de la entidad no pueden estar vacíos.");  
                // Retorna un error HTTP 400 si algún parámetro requerido está vacío.

            try
            {
                // Convierte los datos recibidos en un diccionario con las claves y valores adecuados.
                var propiedades = datosEntidad.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value is JsonElement elementoJson 
                        ? ConvertirJsonElement(elementoJson) // Convierte valores JSON a tipos de datos de C#
                        : kvp.Value 
                );

                // Definir una lista de posibles nombres de claves que representan contraseñas.
                var clavesContrasena = new[] { "password", "contrasena", "passw", "clave" };

                // Verifica si alguno de los campos en los datos coincide con un posible campo de contraseña.
                var claveContrasena = propiedades.Keys.FirstOrDefault(k => 
                    clavesContrasena.Any(pk => k.IndexOf(pk, StringComparison.OrdinalIgnoreCase) >= 0)
                );

                // Si se encuentra un campo de contraseña, se procede a cifrarla antes de almacenarla en la BD.
                if (claveContrasena != null)
                {
                    var contrasenaPlano = propiedades[claveContrasena]?.ToString();
                    if (!string.IsNullOrEmpty(contrasenaPlano))
                    {
                        propiedades[claveContrasena] = BCrypt.Net.BCrypt.HashPassword(contrasenaPlano);
                        // Se cifra la contraseña usando BCrypt.
                    }
                }

                // Obtiene el proveedor de base de datos desde la configuración.
                string proveedor = _configuration["DatabaseProvider"] ?? 
                    throw new InvalidOperationException("Proveedor de base de datos no configurado.");

                // Construye la lista de columnas y valores a insertar en la tabla.
                var columnas = string.Join(",", propiedades.Keys);
                var valores = string.Join(",", propiedades.Keys.Select(k => $"{ObtenerPrefijoParametro(proveedor)}{k}"));

                // Genera la consulta SQL de inserción.
                string consultaSQL = $"INSERT INTO {nombreTabla} ({columnas}) VALUES ({valores})";

                // Crea los parámetros para la consulta SQL.
                var parametros = propiedades.Select(p => 
                    CrearParametro($"{ObtenerPrefijoParametro(proveedor)}{p.Key}", p.Value)
                ).ToArray();

                // Muestra en la consola la consulta SQL generada y los parámetros.
                Console.WriteLine($"Ejecutando consulta SQL: {consultaSQL} con parámetros:");
                foreach (var parametro in parametros)
                {
                    Console.WriteLine($"{parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}");
                }

                // Abre la conexión a la base de datos y ejecuta la inserción.
                controlConexion.AbrirBd();
                controlConexion.EjecutarComandoSql(consultaSQL, parametros);
                controlConexion.CerrarBd();

                // Retorna un mensaje de éxito indicando que la entidad se creó correctamente.
                return Ok("Entidad creada exitosamente.");
            }
            catch (Exception ex) // Captura cualquier error inesperado.
            {
                Console.WriteLine($"Ocurrió una excepción: {ex.Message}"); // Imprime el error en la consola.
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); 
                // Retorna un error HTTP 500 indicando que ocurrió un problema en el servidor.
            }
        }


        // Método privado para convertir un JsonElement en su tipo correspondiente.
        private object? ConvertirJsonElement(JsonElement elementoJson)
        {
            if (elementoJson.ValueKind == JsonValueKind.Null)
                return null; // Si el valor es nulo, retorna null.

            switch (elementoJson.ValueKind)
            {
                case JsonValueKind.String:
                    // Intenta convertir la cadena a un valor de tipo DateTime, si falla, retorna la cadena original.
                    return DateTime.TryParse(elementoJson.GetString(), out DateTime valorFecha) ? (object)valorFecha : elementoJson.GetString();
                case JsonValueKind.Number:
                    // Intenta convertir el número a un valor entero, si falla, retorna el valor como doble.
                    return elementoJson.TryGetInt32(out var valorEntero) ? (object)valorEntero : elementoJson.GetDouble();
                case JsonValueKind.True:
                    return true; // Retorna verdadero si el valor es de tipo booleano verdadero.
                case JsonValueKind.False:
                    return false; // Retorna falso si el valor es de tipo booleano falso.
                case JsonValueKind.Null:
                    return null; // Retorna null si el valor es nulo.
                case JsonValueKind.Object:
                    return elementoJson.GetRawText(); // Retorna el texto crudo del objeto JSON.
                case JsonValueKind.Array:
                    return elementoJson.GetRawText(); // Retorna el texto crudo del arreglo JSON.
                default:
                    // Lanza una excepción si el tipo de valor JSON no está soportado.
                    throw new InvalidOperationException($"Tipo de JsonValueKind no soportado: {elementoJson.ValueKind}");
            }
        }

        // Método privado para obtener el prefijo adecuado para los parámetros SQL, según el proveedor de la base de datos.
        private string ObtenerPrefijoParametro(string proveedor)
        {
            return "@"; // Para SQL Server y LocalDB, el prefijo es "@". En caso de otros proveedores, se pueden agregar más condiciones aquí.
        }


        /// <summary>
        /// Actualiza un registro específico en la tabla de la base de datos basado en una clave y su valor.
        /// </summary>
        /// <param name="nombreProyecto">Nombre del proyecto al que pertenece la tabla.</param>
        /// <param name="nombreTabla">Nombre de la tabla en la base de datos.</param>
        /// <param name="nombreClave">Nombre de la columna clave utilizada para la búsqueda.</param>
        /// <param name="valorClave">Valor de la clave para identificar el registro a actualizar.</param>
        /// <param name="datosEntidad">Diccionario con los datos de la entidad que se actualizarán.</param>
        /// <returns>Un mensaje de éxito si la actualización es correcta, o un código de error en caso de fallo.</returns>
        /// <response code="200">Devuelve un mensaje indicando que la entidad fue actualizada exitosamente.</response>
        /// <response code="400">El nombre de la tabla, la clave o los datos de la entidad están vacíos.</response>
        /// <response code="500">Error interno del servidor.</response>
        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpPut("{nombreClave}/{valorClave}")] // Define una ruta HTTP PUT con parámetros adicionales.
        public IActionResult Actualizar(string nombreProyecto, string nombreTabla, string nombreClave, string valorClave, [FromBody] Dictionary<string, object?> datosEntidad) // Actualiza una fila en la tabla basada en una clave.
        {
            if (string.IsNullOrWhiteSpace(nombreTabla) || string.IsNullOrWhiteSpace(nombreClave) || datosEntidad == null || !datosEntidad.Any()) // Verifica si alguno de los parámetros está vacío.
                return BadRequest("El nombre de la tabla, el nombre de la clave y los datos de la entidad no pueden estar vacíos."); // Retorna un error si algún parámetro está vacío.

            try
            {
                var propiedades = datosEntidad.ToDictionary( // Convierte los datos de la entidad en un diccionario de propiedades.
                    kvp => kvp.Key,
                    kvp => kvp.Value is JsonElement elementoJson ? ConvertirJsonElement(elementoJson) : kvp.Value);

                // Verifica si hay un campo de contraseña en los datos, y si lo hay, lo hashea.
                var clavesContrasena = new[] { "password", "contrasena", "passw", "clave" }; // Lista de posibles nombres para campos de contraseña.
                var claveContrasena = propiedades.Keys.FirstOrDefault(k => clavesContrasena.Any(pk => k.IndexOf(pk, StringComparison.OrdinalIgnoreCase) >= 0)); // Busca si alguno de los campos es una contraseña.
                
                if (claveContrasena != null) // Si se encontró un campo de contraseña.
                {
                    var contrasenaPlano = propiedades[claveContrasena]?.ToString(); // Obtiene el valor de la contraseña.
                    if (!string.IsNullOrEmpty(contrasenaPlano)) // Si la contraseña no está vacía.
                    {
                        propiedades[claveContrasena] = BCrypt.Net.BCrypt.HashPassword(contrasenaPlano); // Hashea la contraseña.
                    }
                }

                string proveedor = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("Proveedor de base de datos no configurado."); // Obtiene el proveedor de base de datos.
                var actualizaciones = string.Join(",", propiedades.Select(p => $"{p.Key}={ObtenerPrefijoParametro(proveedor)}{p.Key}")); // Crea la cadena de actualizaciones para la consulta SQL.
                string consultaSQL = $"UPDATE {nombreTabla} SET {actualizaciones} WHERE {nombreClave}={ObtenerPrefijoParametro(proveedor)}ValorClave"; // Crea la consulta SQL para actualizar la fila.

                var parametros = propiedades.Select(p => CrearParametro($"{ObtenerPrefijoParametro(proveedor)}{p.Key}", p.Value)).ToList(); // Crea los parámetros para la consulta SQL.
                parametros.Add(CrearParametro($"{ObtenerPrefijoParametro(proveedor)}ValorClave", valorClave)); // Agrega el parámetro para la clave de la fila a actualizar.

                Console.WriteLine($"Ejecutando consulta SQL: {consultaSQL} con parámetros:"); // Muestra la consulta SQL y los parámetros en la consola.
                foreach (var parametro in parametros) // Recorre cada parámetro.
                {
                    Console.WriteLine($"{parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}"); // Muestra el nombre y valor del parámetro en la consola.
                }

                controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
                controlConexion.EjecutarComandoSql(consultaSQL, parametros.ToArray()); // Ejecuta la consulta SQL para actualizar la fila.
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.

                return Ok("Entidad actualizada exitosamente."); // Retorna una respuesta de éxito.
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                Console.WriteLine($"Ocurrió una excepción: {ex.Message}"); // Muestra el mensaje de la excepción en la consola.
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción.
            }
        }


        /// <summary>
        /// Elimina un registro específico de la tabla de la base de datos basado en una clave y su valor.
        /// </summary>
        /// <param name="nombreProyecto">Nombre del proyecto al que pertenece la tabla.</param>
        /// <param name="nombreTabla">Nombre de la tabla en la base de datos.</param>
        /// <param name="nombreClave">Nombre de la columna clave utilizada para identificar el registro.</param>
        /// <param name="valorClave">Valor de la clave que identifica el registro a eliminar.</param>
        /// <returns>Un mensaje de éxito si la eliminación es correcta, o un código de error en caso de fallo.</returns>
        /// <response code="200">Devuelve un mensaje indicando que la entidad fue eliminada exitosamente.</response>
        /// <response code="400">El nombre de la tabla o el nombre de la clave están vacíos.</response>
        /// <response code="500">Error interno del servidor.</response>
        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpDelete("{nombreClave}/{valorClave}")] // Define una ruta HTTP DELETE con parámetros adicionales.
        public IActionResult Eliminar(string nombreProyecto, string nombreTabla, string nombreClave, string valorClave) // Elimina una fila de la tabla basada en una clave.
        {
            if (string.IsNullOrWhiteSpace(nombreTabla) || string.IsNullOrWhiteSpace(nombreClave)) // Verifica si alguno de los parámetros está vacío.
                return BadRequest("El nombre de la tabla o el nombre de la clave no pueden estar vacíos."); // Retorna un error si algún parámetro está vacío.

            try
            {
                string proveedor = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("Proveedor de base de datos no configurado."); // Obtiene el proveedor de base de datos.
                string consultaSQL = $"DELETE FROM {nombreTabla} WHERE {nombreClave}=@ValorClave"; // Crea la consulta SQL para eliminar la fila.
                var parametro = CrearParametro("@ValorClave", valorClave); // Crea el parámetro para la clave de la fila a eliminar.

                controlConexion.AbrirBd(); // Abre la conexión a la base de datos.
                controlConexion.EjecutarComandoSql(consultaSQL, new[] { parametro }); // Ejecuta la consulta SQL para eliminar la fila.
                controlConexion.CerrarBd(); // Cierra la conexión a la base de datos.

                return Ok("Entidad eliminada exitosamente."); // Retorna una respuesta de éxito.
            }
            catch (Exception ex) // Captura cualquier excepción que ocurra durante la ejecución.
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción.
            }
        }


        /// <summary>
        /// Verifica si la contraseña proporcionada coincide con la contraseña almacenada en la base de datos para un usuario específico.
        /// </summary>
        /// <param name="nombreProyecto">Nombre del proyecto al que pertenece la tabla.</param>
        /// <param name="nombreTabla">Nombre de la tabla en la base de datos que almacena los usuarios.</param>
        /// <param name="datos">Diccionario con los campos necesarios para la verificación:
        /// <list type="bullet">
        /// <item><description><c>campoUsuario</c>: Nombre de la columna que almacena el usuario.</description></item>
        /// <item><description><c>campoContrasena</c>: Nombre de la columna que almacena la contraseña hasheada.</description></item>
        /// <item><description><c>valorUsuario</c>: Nombre de usuario proporcionado en la solicitud.</description></item>
        /// <item><description><c>valorContrasena</c>: Contraseña en texto plano proporcionada en la solicitud.</description></item>
        /// </list>
        /// </param>
        /// <returns>Un mensaje indicando el resultado de la verificación.</returns>
        /// <response code="200">La contraseña es correcta y coincide con la almacenada en la base de datos.</response>
        /// <response code="400">Faltan parámetros obligatorios en la solicitud.</response>
        /// <response code="404">El usuario no fue encontrado en la base de datos.</response>
        /// <response code="401">La contraseña es incorrecta.</response>
        /// <response code="500">Error interno del servidor.</response>
        [AllowAnonymous] // Permite el acceso sin autenticación a este endpoint.
        [HttpPost("verificar-contrasena")] // Define la ruta HTTP POST como "/api/{nombreProyecto}/{nombreTabla}/verificar-contrasena".
        public IActionResult VerificarContrasena(string nombreProyecto, string nombreTabla, [FromBody] Dictionary<string, string> datos) 
        // Método que verifica si la contraseña ingresada coincide con la almacenada en la base de datos.
        {
            // Verifica que los parámetros esenciales no sean nulos o vacíos.
            if (string.IsNullOrWhiteSpace(nombreTabla) || datos == null || 
                !datos.ContainsKey("campoUsuario") || !datos.ContainsKey("campoContrasena") || 
                !datos.ContainsKey("valorUsuario") || !datos.ContainsKey("valorContrasena"))
            {
                return BadRequest("El nombre de la tabla, el campo de usuario, el campo de contraseña, el valor de usuario y el valor de contraseña no pueden estar vacíos.");
            }

            try
            {
                // Extrae los valores necesarios del diccionario recibido en la solicitud.
                string campoUsuario = datos["campoUsuario"]; // Nombre de la columna que almacena el usuario.
                string campoContrasena = datos["campoContrasena"]; // Nombre de la columna que almacena la contraseña hasheada.
                string valorUsuario = datos["valorUsuario"]; // Usuario proporcionado en la solicitud.
                string valorContrasena = datos["valorContrasena"]; // Contraseña en texto plano proporcionada en la solicitud.

                // Obtiene el proveedor de la base de datos desde la configuración.
                string proveedor = _configuration["DatabaseProvider"] ?? throw new InvalidOperationException("Proveedor de base de datos no configurado.");

                // Construye la consulta SQL para obtener la contraseña hasheada del usuario especificado.
                string consultaSQL = $"SELECT {campoContrasena} FROM {nombreTabla} WHERE {campoUsuario} = @ValorUsuario";

                // Crea el parámetro de la consulta con el valor del usuario.
                var parametro = CrearParametro("@ValorUsuario", valorUsuario);

                // Abre la conexión con la base de datos.
                controlConexion.AbrirBd();

                // Ejecuta la consulta SQL para obtener la contraseña almacenada en la base de datos.
                var resultado = controlConexion.EjecutarConsultaSql(consultaSQL, new DbParameter[] { parametro });

                // Cierra la conexión con la base de datos.
                controlConexion.CerrarBd();

                // Verifica si la consulta no devolvió resultados (usuario no encontrado).
                if (resultado.Rows.Count == 0)
                {
                    return NotFound("Usuario no encontrado."); // Retorna un error 404 si el usuario no existe en la base de datos.
                }

                // Obtiene la contraseña almacenada en la base de datos.
                string contrasenaHasheada = resultado.Rows[0][campoContrasena]?.ToString() ?? string.Empty;

                // Verifica que el valor almacenado sea un hash válido de BCrypt (debe comenzar con "$2").
                if (!contrasenaHasheada.StartsWith("$2"))
                {
                    throw new InvalidOperationException("El hash de la contraseña almacenada no es un hash válido de BCrypt.");
                }

                // Compara la contraseña en texto plano con el hash almacenado utilizando BCrypt.
                bool esContrasenaValida = BCrypt.Net.BCrypt.Verify(valorContrasena, contrasenaHasheada);

                // Si la contraseña es correcta, retorna un mensaje de éxito.
                if (esContrasenaValida)
                {
                    return Ok("Contraseña verificada exitosamente.");
                }
                else
                {
                    return Unauthorized("Contraseña incorrecta."); // Retorna un error 401 si la contraseña no coincide.
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ocurrió una excepción: {ex.Message}"); // Muestra en la consola el error si ocurre una excepción.
                return StatusCode(500, $"Error interno del servidor: {ex.Message}"); // Retorna un error 500 si ocurre una excepción inesperada.
            }
        }


        /// <summary>
        /// Ejecuta una consulta SQL parametrizada recibida en el cuerpo de la solicitud.
        /// </summary>
        /// <param name="cuerpoSolicitud">Objeto JSON que contiene la consulta SQL y los parámetros.</param>
        /// <returns>Un conjunto de resultados en formato JSON o un mensaje de error en caso de fallo.</returns>
        /// <response code="200">Devuelve los resultados de la consulta en formato JSON.</response>
        /// <response code="400">La consulta SQL no es válida o está vacía.</response>
        /// <response code="404">No se encontraron resultados para la consulta proporcionada.</response>
        /// <response code="500">Error en la base de datos o error interno del servidor.</response>
        [AllowAnonymous] // Permite el acceso anónimo a este método.
        [HttpPost("ejecutar-consulta-parametrizada")] // Define la ruta HTTP POST como "/api/{nombreProyecto}/{nombreTabla}/ejecutar-consulta-parametrizada".
        public IActionResult EjecutarConsultaParametrizada([FromBody] JsonElement cuerpoSolicitud)
        {
            try
            {
                // Verifica si el cuerpo de la solicitud contiene la consulta SQL
                if (!cuerpoSolicitud.TryGetProperty("consulta", out var consultaElement) || consultaElement.ValueKind != JsonValueKind.String)
                {
                    return BadRequest("Debe proporcionar una consulta SQL válida en el cuerpo de la solicitud.");
                }

                string consultaSQL = consultaElement.GetString() ?? throw new ArgumentException("La consulta SQL no puede estar vacía.");

                // Verifica si el cuerpo de la solicitud contiene los parámetros
                var parametros = new List<DbParameter>();
                if (cuerpoSolicitud.TryGetProperty("parametros", out var parametrosElement) && parametrosElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var parametro in parametrosElement.EnumerateObject())
                    {
                        string paramName = parametro.Name.StartsWith("@") ? parametro.Name : "@" + parametro.Name;
                        object? paramValue = parametro.Value.ValueKind == JsonValueKind.Null ? DBNull.Value : parametro.Value.GetRawText().Trim('"');
                        parametros.Add(controlConexion.CrearParametro(paramName, paramValue));
                    }
                }

                // Abrir la conexión a la base de datos
                controlConexion.AbrirBd();

                // Ejecutar la consulta SQL
                var resultado = controlConexion.EjecutarConsultaSql(consultaSQL, parametros.ToArray());

                // Cerrar la conexión a la base de datos
                controlConexion.CerrarBd();

                // Verifica si hay resultados
                if (resultado.Rows.Count == 0)
                {
                    return NotFound("No se encontraron resultados para la consulta proporcionada.");
                }

                // Procesar resultados a formato JSON
                var lista = new List<Dictionary<string, object?>>();
                foreach (DataRow fila in resultado.Rows)
                {
                    var propiedades = resultado.Columns.Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => fila[col] == DBNull.Value ? null : fila[col]);
                    lista.Add(propiedades);
                }

                // Retornar resultados en formato JSON
                return Ok(lista);
            }
            catch (SqlException sqlEx)
            {
                // Manejo de excepciones SQL
                controlConexion.CerrarBd(); // Asegura que la conexión se cierre en caso de error
                Console.WriteLine($"SQL Error: {sqlEx.Message}");
                return StatusCode(500, new { Mensaje = "Error en la base de datos.", Detalle = sqlEx.Message });
            }
            catch (Exception ex)
            {
                // Manejo de excepciones generales
                controlConexion.CerrarBd(); // Asegura que la conexión se cierre en caso de error
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new { Mensaje = "Se presentó un error:", Detalle = ex.Message });
            }
        }

}
}

/*
Modos de uso:

GET
http://localhost:5184/api/proyecto/usuario
http://localhost:5184/api/proyecto/usuario/email/admin@empresa.com

POST
http://localhost:5184/api/proyecto/usuario/
{
    "email": "nuevo.nuevo@empresa.com",
    "contrasena": "123"
}

PUT
http://localhost:5184/api/proyecto/usuario/email/nuevo.nuevo@empresa.com
{
    "contrasena": "456"
}

DELETE
http://localhost:5184/api/proyecto/usuario/email/nuevo.nuevo@empresa.com
*/
/*
Códigos de estado HTTP:

2xx (Éxito):
- 200 OK: La solicitud ha tenido éxito.
- 201 Creado: La solicitud ha sido completada y ha resultado en la creación de un nuevo recurso.
- 202 Aceptado: La solicitud ha sido aceptada para procesamiento, pero el procesamiento no ha sido completado.
- 203 Información no autoritativa: La respuesta se ha obtenido de una copia en caché en lugar de directamente del servidor original.
- 204 Sin contenido: La solicitud ha tenido éxito pero no hay contenido que devolver.
- 205 Restablecer contenido: La solicitud ha tenido éxito, pero el cliente debe restablecer la vista que ha solicitado.
- 206 Contenido parcial: El servidor está enviando una respuesta parcial del recurso debido a una solicitud Range.

3xx (Redirección):
- 300 Múltiples opciones: El servidor puede responder con una de varias opciones.
- 301 Movido permanentemente: El recurso solicitado ha sido movido de manera permanente a una nueva URL.
- 302 Encontrado: El recurso solicitado reside temporalmente en una URL diferente.
- 303 Ver otros: El servidor dirige al cliente a una URL diferente para obtener la respuesta solicitada (usualmente en una operación POST).
- 304 No modificado: El contenido no ha cambiado desde la última solicitud (usualmente usado con la caché).
- 305 Usar proxy: El recurso solicitado debe ser accedido a través de un proxy.
- 307 Redirección temporal: Similar al 302, pero el cliente debe utilizar el mismo método de solicitud original (GET o POST).
- 308 Redirección permanente: Similar al 301, pero el método de solicitud original debe ser utilizado en la nueva URL.

4xx (Errores del cliente):
- 400 Solicitud incorrecta: La solicitud contiene sintaxis errónea o no puede ser procesada.
- 401 No autorizado: El cliente debe autenticarse para obtener la respuesta solicitada.
- 402 Pago requerido: Este código es reservado para uso futuro, generalmente relacionado con pagos.
- 403 Prohibido: El cliente no tiene permisos para acceder al recurso, incluso si está autenticado.
- 404 No encontrado: El servidor no pudo encontrar el recurso solicitado.
- 405 Método no permitido: El método HTTP utilizado no está permitido para el recurso solicitado.
- 406 No aceptable: El servidor no puede generar una respuesta que coincida con las características aceptadas por el cliente.
- 407 Autenticación de proxy requerida: Similar a 401, pero la autenticación debe hacerse a través de un proxy.
- 408 Tiempo de espera agotado: El cliente no envió una solicitud dentro del tiempo permitido por el servidor.
- 409 Conflicto: La solicitud no pudo ser completada debido a un conflicto en el estado actual del recurso.
- 410 Gone: El recurso solicitado ya no está disponible y no será vuelto a crear.
- 411 Longitud requerida: El servidor requiere que la solicitud especifique una longitud en los encabezados.
- 412 Precondición fallida: Una condición en los encabezados de la solicitud falló.
- 413 Carga útil demasiado grande: El cuerpo de la solicitud es demasiado grande para ser procesado.
- 414 URI demasiado largo: La URI solicitada es demasiado larga para que el servidor la procese.
- 415 Tipo de medio no soportado: El formato de los datos en la solicitud no es compatible con el servidor.
- 416 Rango no satisfactorio: La solicitud incluye un rango que no puede ser satisfecho.
- 417 Fallo en la expectativa: La expectativa indicada en los encabezados de la solicitud no puede ser cumplida.
- 418 Soy una tetera (RFC 2324): Este código es un Easter Egg HTTP. El servidor rechaza la solicitud porque "soy una tetera."
- 421 Mala asignación: El servidor no puede cumplir con la solicitud.
- 426 Se requiere actualización: El cliente debe actualizar el protocolo de solicitud.
- 428 Precondición requerida: El servidor requiere que se cumpla una precondición antes de procesar la solicitud.
- 429 Demasiadas solicitudes: El cliente ha enviado demasiadas solicitudes en un corto periodo de tiempo.
- 431 Campos de encabezado muy grandes: Los campos de encabezado de la solicitud son demasiado grandes.
- 451 No disponible por razones legales: El contenido ha sido bloqueado por razones legales (ej. leyes de copyright).

5xx (Errores del servidor):
- 500 Error interno del servidor: El servidor encontró una situación inesperada que le impidió completar la solicitud.
- 501 No implementado: El servidor no tiene la capacidad de completar la solicitud.
- 502 Puerta de enlace incorrecta: El servidor, al actuar como puerta de enlace o proxy, recibió una respuesta no válida del servidor upstream.
- 503 Servicio no disponible: El servidor no está disponible temporalmente, generalmente debido a mantenimiento o sobrecarga.
- 504 Tiempo de espera de la puerta de enlace: El servidor, al actuar como puerta de enlace o proxy, no recibió una respuesta a tiempo de otro servidor.
- 505 Versión HTTP no soportada: El servidor no soporta la versión HTTP utilizada en la solicitud.
- 506 Variante también negocia: El servidor encontró una referencia circular al negociar el contenido.
- 507 Almacenamiento insuficiente: El servidor no puede almacenar la representación necesaria para completar la solicitud.
- 508 Bucle detectado: El servidor detectó un bucle infinito al procesar la solicitud.
- 510 No extendido: Se requiere la extensión adicional de las políticas de acceso.
- 511 Se requiere autenticación de red: El cliente debe autenticar la red para poder acceder al recurso.
*/
