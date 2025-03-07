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

                // Obtiene el tipo de dato de la columna en la base de datos, asegurando que no genere errores si el valor es nulo.
                string tipoDato = resultadoTipoDato.Rows[0]["data_type"]?.ToString() ?? "";

                /*
                Explicación de cada parte de la línea:

                1️⃣ `resultadoTipoDato.Rows[0]` 
                - Accede a la primera fila del resultado de la consulta SQL.
                - `resultadoTipoDato` es un `DataTable` que almacena los resultados de la consulta.

                2️⃣ `["data_type"]`
                - Obtiene el valor de la columna "data_type" en la primera fila.
                - Esta columna contiene el tipo de dato de la columna consultada en la base de datos.
                - Ejemplo de valores posibles: "int", "varchar", "datetime", etc.

                3️⃣ `?.ToString()`
                - `?.` (Operador de propagación de nulos): 
                    - Si el valor en la columna es `null`, evita errores y retorna `null` en lugar de intentar convertirlo a cadena.
                - `.ToString()`
                    - Convierte el valor en cadena si no es `null`.

                4️⃣ `?? ""`
                - (Operador de coalescencia nula): 
                    - Si el valor después de `.ToString()` es `null`, asigna una cadena vacía `""`.
                - Esto evita que `tipoDato` tenga un valor `null`, asegurando que siempre contenga una cadena.

                📌 **Ejemplo de comportamiento**:
                - Si `data_type = "int"` → `tipoDato` será `"int"`.
                - Si `data_type = NULL` → `tipoDato` será `""` (cadena vacía).
                - Si la consulta falla y no encuentra la columna, se debe manejar antes de acceder a `Rows[0]` para evitar errores.

                ⚠ **Recomendación**: Siempre verificar que `resultadoTipoDato.Rows.Count > 0` antes de acceder a `Rows[0]`.
                */

                Console.WriteLine($"Tipo de dato detectado para la columna {nombreClave}: {tipoDato}");

                if (string.IsNullOrEmpty(tipoDato)) // Verifica si el tipo de dato es válido.
                {
                    return NotFound("No se pudo determinar el tipo de dato."); // Retorna una respuesta de error si el tipo de dato es inválido.
                }

                object valorConvertido;
                string comandoSQL;

                // Determina cómo tratar el valor y la consulta SQL según el tipo de dato, compatible con SQL Server y LocalDB.
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
                // "datosEntidad" es un Dictionary<string, object?> que contiene los datos enviados en la solicitud HTTP POST.
                // Se utiliza el método ToDictionary() para transformar el diccionario original y asegurarse de que los valores sean manejables en C#.
                var propiedades = datosEntidad.ToDictionary(
                    kvp => kvp.Key, // La clave del diccionario original se mantiene sin cambios.
                    
                    // Verifica si el valor en el diccionario es un tipo JsonElement (lo que ocurre cuando los datos se reciben en formato JSON).
                    kvp => kvp.Value is JsonElement elementoJson 
                        ? ConvertirJsonElement(elementoJson) // Si el valor es un JsonElement, lo convierte a un tipo de dato apropiado en C#.
                        : kvp.Value // Si el valor no es un JsonElement, se deja tal cual.
                );


                // Definir una lista de posibles nombres de claves que representan contraseñas.
                var clavesContrasena = new[] { "password", "contrasena", "passw", "clave" };

                // Verifica si alguno de los campos en los datos coincide con un posible campo de contraseña.
                // "propiedades.Keys" representa la lista de nombres de los campos recibidos en los datos de la entidad.
                // Se usa FirstOrDefault() para encontrar el primer campo que coincida con algún nombre típico de contraseña.
                var claveContrasena = propiedades.Keys.FirstOrDefault(k => 
                    // Recorre la lista de posibles nombres de contraseñas y verifica si alguno de ellos está en el nombre del campo actual.
                    clavesContrasena.Any(pk => k.IndexOf(pk, StringComparison.OrdinalIgnoreCase) >= 0)
                );


                // Si se encuentra un campo de contraseña, se procede a cifrarla antes de almacenarla en la BD.
                if (claveContrasena != null)
                {
                    var contrasenaPlano = propiedades[claveContrasena]?.ToString(); // Obtiene el valor en texto plano de la contraseña.
                    if (!string.IsNullOrEmpty(contrasenaPlano))
                    {
                        propiedades[claveContrasena] = BCrypt.Net.BCrypt.HashPassword(contrasenaPlano); 
                        // Se cifra la contraseña usando BCrypt.
                    }
                }

                // Obtiene el proveedor de base de datos desde la configuración.
                string proveedor = _configuration["DatabaseProvider"] ?? 
                    throw new InvalidOperationException("Proveedor de base de datos no configurado.");

                // Construye la lista de columnas a insertar en la tabla.
                var columnas = string.Join(",", propiedades.Keys); 

                // Construye la lista de valores con sus correspondientes parámetros para la consulta SQL.
                var valores = string.Join(",", propiedades.Keys.Select(k => $"{ObtenerPrefijoParametro(proveedor)}{k}"));

                // Genera la consulta SQL de inserción usando las columnas y valores preparados.
                string consultaSQL = $"INSERT INTO {nombreTabla} ({columnas}) VALUES ({valores})";

                // Crea los parámetros para la consulta SQL, asignando los valores correspondientes.
                var parametros = propiedades.Select(p => 
                    CrearParametro($"{ObtenerPrefijoParametro(proveedor)}{p.Key}", p.Value)
                ).ToArray();

                // Muestra en la consola la consulta SQL generada y los parámetros para depuración.
                Console.WriteLine($"Ejecutando consulta SQL: {consultaSQL} con parámetros:");
                foreach (var parametro in parametros)
                {
                    Console.WriteLine($"{parametro.ParameterName} = {parametro.Value}, DbType: {parametro.DbType}");
                }

                // Abre la conexión a la base de datos.
                controlConexion.AbrirBd();

                // Ejecuta el comando SQL para insertar los datos en la tabla.
                controlConexion.EjecutarComandoSql(consultaSQL, parametros);

                // Cierra la conexión a la base de datos para liberar recursos.
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

