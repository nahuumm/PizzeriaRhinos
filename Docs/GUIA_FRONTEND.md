# Guía del frontend — Rhinos Pizza POS

Documento del rol **Frontend**: qué se cambió, cómo integrarlo paso a paso, cómo cumple el *Proyecto Final* y el *Análisis del Proyecto*, y qué pruebas ejecutar.

---

## 1. Integración paso a paso

### Paso 1 — Respaldar y actualizar tu rama
```bash
cd PizzeriaRhinos
git checkout frontend
git pull origin main          # trae lo último de backend y DBA
git status                    # debe estar limpio (si no, haz commit o stash)
```

### Paso 2 — Copiar los archivos nuevos
Copia el contenido del ZIP encima de tu carpeta `PizzeriaRhinos` (reemplaza cuando te pregunte).

**Archivos eliminados** (bórralos de tu carpeta si siguen ahí):
- `Aplicacion/POS_PizzeriaRhinos/Form1.cs`, `Form1.Designer.cs`, `Form1.resx` (formulario vacío de plantilla)
- `Aplicacion/POS_PizzeriaRhinos/Formularios/FrmPrincipalPOS.cs` y `FrmPrincipalPOS.resx` (reemplazado por `FrmPrincipal` + `Vistas/`)

**Archivos nuevos**
| Carpeta | Archivos |
| --- | --- |
| `Controles/` | `Controles.cs`, `TecladoVirtual.cs`, `TecladoNumerico.cs` |
| `Vistas/` | `VistaBase.cs`, `VistaDashboard.cs`, `VistaPuntoVenta.cs`, `VistaPedidos.cs`, `VistaClientes.cs`, `VistaInventario.cs`, `VistaEmpleados.cs`, `VistaReportes.cs` |
| `Formularios/` | `FrmPrincipal.cs`, `FrmConfigurarPizza.cs`, `FrmCobro.cs`, `FrmCliente.cs`, `FrmDetallePedido.cs`, `FrmEmpleado.cs`, `Dialogo.cs` |
| `Impresion/` | `ImpresorTickets.cs` |
| `Datos/` | `ProductoDAO.cs`, `IngredienteDAO.cs`, `ReporteDAO.cs` |
| `Modelos/` | `Sesion.cs`, `ReportesModelos.cs` |
| `Recursos/` | `logo_rhinos.png` (logo con fondo transparente), `rhinos.ico` |
| `Docs/` | `GUIA_FRONTEND.md` |

**Archivos modificados**
| Archivo | Cambio |
| --- | --- |
| `Program.cs` | Ciclo login → ventana principal → cerrar sesión; manejo global de errores |
| `Formularios/UITheme.cs` | Nuevo sistema de diseño (paleta de pizzería, tipografía, escala DPI, tablas) |
| `Formularios/FrmLogin.cs` | Rediseño con logo; **se quitaron las credenciales precargadas**; indicador de conexión |
| `Datos/EmpleadoDAO.cs` | Se agregó el CRUD de empleados (los métodos existentes no cambiaron) |
| `Datos/PedidoDAO.cs` | `ObtenerDetallePedido` ahora también devuelve la columna `Categoria` (para la comanda). Nada más cambió. |
| `Modelos/ItemCarrito.cs` | Nueva propiedad informativa `PrecioLista` |
| `POS_PizzeriaRhinos.csproj` | Ícono de la app y copia de `Recursos/` a la carpeta de salida |
| `README.md` | Requisitos, instalación, configuración y ejecución |

> ⚠ **Aviso para backend:** se tocaron `EmpleadoDAO.cs` y `PedidoDAO.cs`, que son de su rol. Los cambios solo **agregan** cosas: métodos de CRUD nuevos y una columna extra en el SELECT del detalle. Revísenlos en el Pull Request.

### Paso 3 — Abrir y compilar
1. Abre `Aplicacion/POS_PizzeriaRhinos/POS_PizzeriaRhinos.slnx`.
2. En el **Explorador de soluciones** verifica que aparezcan las carpetas `Controles`, `Vistas`, `Impresion` y `Recursos` (los proyectos SDK incluyen los archivos automáticamente).
3. **Compilar → Limpiar solución** y luego **Compilar → Recompilar solución**.
4. Si Visual Studio intenta abrir un formulario en el *diseñador*, presiona **F7** para ver el código. Todos los formularios se construyen por código y llevan `[DesignerCategory("Code")]`.
5. Si aparece un error, copia el mensaje exacto (archivo y línea) para corregirlo.

### Paso 4 — Verificar la conexión
- Configura los **secretos de usuario** como dice el `README.md`.
- Ejecuta con **F5**. En el login, abajo, debe salir **● Conectado a la base de datos pizzeria_rhinos**.

### Paso 5 — Recorrido de prueba rápido (5 minutos)
1. Entra con `cajera1` / `caja123` → en el menú lateral **no** aparecen Inventario, Empleados ni Reportes.
2. **Nuevo pedido** → *A domicilio* → teléfono `6461112233` → **Buscar** → aparece María Fernanda Castro.
3. Toca **Armar pizza** → tamaño *Grande* → *Sabor*: *Mitad y mitad* Hawaiana / Mexicana → agrega 3 ingredientes CON y revisa que el contador indique cuándo empieza a cobrar → *Masa y orilla*: sartén + orilla de queso → **Agregar al ticket**. Luego **Armar calzone** → *Mediano* → quita un ingrediente → queso extra.
4. **Bebidas** → Manzanita 600 ml (stock 2): agrégala 3 veces → la 3.ª vez debe avisar que no hay existencias.
5. **Cobrar** → efectivo $1,000 → **Confirmar** → imprime la comanda y el ticket (vista previa).
6. **Pedidos** → selecciona el folio → elige repartidor → **Despachar a ruta** → **Marcar entregado**.
7. Cierra sesión, entra con `admin` / `admin123` → **Reportes** → *Corte de caja* y *Liquidación de repartidores*.

### Paso 6 — Commit y Pull Request
```bash
git add -A
git commit -m "(frontend): rediseño POS con botones grandes, roles, configurador de pizza, despacho, inventario, empleados, reportes e impresión"
git push origin frontend
```
Abre el Pull Request `frontend → main` en GitHub y pide revisión a backend (por los DAOs) y a DBA.

### Paso 7 — Evidencias para el reporte (punto 38)
Toma capturas de: login, inicio (cajera y admin), nuevo pedido con ticket, cada paso del configurador, cobro, comanda y ticket, pedidos con estados de color, clientes con historial, inventario (HAY / NO HAY y bebidas agotadas), empleados, reportes y un mensaje de error de validación.

---

## 2. Diseño (adaptado de Veterinaria a escritorio)

| En Veterinaria (CSS) | En Rhinos (WinForms) |
| --- | --- |
| Variables `:root { --vet-* }` | `UITheme` (paleta, tipografía, radios) |
| `.vet-page-header` con degradado | `EncabezadoModulo` rojo con patrón de cuadros de pizzería |
| `.vet-surface-card` | `PanelRedondeado` |
| `.vet-btn-primary` | `BotonRhino` (estilos Primario, Secundario, Éxito, Peligro, Ámbar, Azul) |
| Alertas `.vet-alert-*` | `Dialogo.Exito / Error / Advertencia / Confirmar` con botones grandes |
| Tablas y estados vacíos | `PanelTabla` con mensaje cuando no hay registros |
| Responsive 390 / 768 / escritorio | Barra lateral compacta (< 1280 px), filas de botones que fluyen, columnas en porcentaje, escala DPI con `UITheme.S()` |

**Paleta** (logo + menús de referencia): rojo `#D62828`, rojo oscuro `#9B1C1C`, ámbar `#F6A51A` (pastillas de precio), crema `#FFF8F0` (fondo), verde `#2E9E5B` (éxito/HAY), azul `#1D63C4` (en ruta, el azul cromado del logo).

**Tamaños táctiles:** botones de 52–74 px de alto, filas de tabla de 44 px, texto de captura de 13–16 pt.

---

## 3. Cumplimiento del Análisis del Proyecto (reglas de negocio)

| # | Regla | Dónde se cumple en la interfaz |
| --- | --- | --- |
| 1 | Cliente único por teléfono, historial, captura con teclado QWERTY | *Nuevo pedido* (Buscar / Nuevo / Historial), `FrmCliente` con `TecladoVirtual`, validación de 10 dígitos; MySQL rechaza duplicados (UNIQUE) |
| 2 | Perfiles sin cocineros; cajera en sesión; repartidor a domicilio | Login solo CAJERO/ADMIN, menú por rol, el pedido guarda a la cajera de la sesión, despacho con `sp_asignar_repartidor_pedido` |
| 3 | SIN / CON, primeros 5 sin costo, desde el 6.° con tarifa por tamaño | Paso *Ingredientes*: SIN sobre la receta, CON solo de ingredientes que no trae la receta (no existe "extra"), contador y costo con `fn_calcular_costo_extra_ingrediente` |
| 4 | Fase final obligatoria | El asistente siempre termina en *Masa y orilla* (masa gruesa/delgada/sartén, orilla, queso extra) con la tarifa del tamaño: Tamaño → Sabor → Ingredientes → Masa y orilla. La masa de **sartén solo se habilita en Grande y Extragrande**. En el calzone el último paso es solo *Queso extra* |
| 5 | 2x1 solo en Mediana/Grande/Extragrande, cada pizza configurable y con mitades | Categoría *Promo 2x1*: configura la pizza 1 y la 2 por separado (mismo tamaño); se cobra la de mayor precio y los extras de ambas |
| 6 | Pizza de mostrador solo en MOSTRADOR | La categoría *Mostrador* solo aparece en mostrador; al cambiar a domicilio se piden quitar esas pizzas |
| 7 | Stock automático de bebidas, botón deshabilitado en 0 | Tarjetas con "Quedan N" / **AGOTADO** deshabilitado; no deja pedir más que el stock; el trigger descuenta |
| 8 | Ingredientes sin gramaje, el dueño los desactiva | *Inventario → Ingredientes* (HAY / NO HAY); en caja aparecen como "Agotado" |
| — | La cocina interactúa solo con la comanda impresa | `ImpresorTickets`: comanda sin precios + ticket del cliente de 80 mm |

## 4. Cumplimiento del Proyecto Final (Etapa 6)

| Requisito | Cumplimiento |
| --- | --- |
| 22. Interfaz navegable con capa de acceso; sin credenciales en el cliente | Ventana principal con navegación; DAOs; cadena de conexión en secretos de usuario |
| 23. CRUD completo de 3 entidades + operación multitabla | **Clientes**, **Empleados** e **Ingredientes** (alta, consulta, edición, baja); productos con edición de precio y activación. Pedido = PEDIDO + DETALLE_PEDIDO + DETALLE_MODIFICADOR + stock en una transacción |
| 24. Reporte con búsqueda/filtros y vista multitabla; mensajes de confirmación, error y estados vacíos | *Reportes* y *Pedidos* sobre `vw_resumen_pedidos_corte` con filtros; `Dialogo.*`; `PanelTabla` con estado vacío |
| 25. Validar obligatorios, formatos, reglas; errores de conexión e integridad | Validaciones en formularios; `ErroresUi.Amigable` traduce 1062 (duplicado), 1451 (FK), 3819 (CHECK), 1644 (SIGNAL de triggers), 1042 (sin conexión) |
| 26. Persistencia | Cada módulo recarga desde MySQL al mostrarse (`AlMostrar`); cerrar sesión / reabrir conserva los datos |

---

## 5. Matriz de pruebas del frontend

> Llena **Resultado observado** y **Estado** al ejecutar cada prueba (no se deben inventar resultados).

| ID | Requisito / regla | Precondición | Datos de entrada | Pasos | Resultado esperado | Resultado observado | Estado |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PF-01 | Login y roles (R2) | BD cargada | `cajera1` / `caja123` | Iniciar sesión | Menú sin Inventario, Empleados ni Reportes | | |
| PF-02 | Login inválido (25) | — | `cajera1` / `xxx` | Iniciar sesión | Mensaje "Usuario o contraseña incorrectos" | | |
| PF-03 | Sin conexión (25) | Detener servicio MySQL80 | — | Abrir la app | Indicador rojo y mensaje claro al intentar entrar | | |
| PF-04 | Alta de cliente con teclado (R1) | — | Tel. `6465556677`, datos completos | Clientes → Añadir, capturar con teclado en pantalla | Cliente guardado y visible en la lista | | |
| PF-05 | Teléfono duplicado (R1, integridad) | Existe `6461112233` | Tel. `6461112233` | Añadir cliente | Mensaje "ya pertenece a otro cliente"; no se guarda | | |
| PF-06 | Formato de teléfono (25) | — | Tel. `64611` | Guardar | Aviso "debe tener exactamente 10 dígitos" | | |
| PF-07 | Eliminar cliente con pedidos (FK) | Cliente 1 tiene pedidos | — | Clientes → Eliminar | Aviso: no se puede eliminar (ON DELETE RESTRICT) | | |
| PF-08 | 6.° ingrediente con costo (R3) | — | Suprema Rhinos Grande + 1 CON | Configurar pizza | El CON muestra costo extra; el contador indica 6 de 5 | | |
| PF-09 | SIN libera lugar (R3) | — | Suprema Rhinos: SIN Cebolla + CON Piña | Configurar pizza | Piña "Sin costo" | | |
| PF-10 | 2x1 solo en Med/Gde/XGde (R5) | — | Promo 2x1 | Abrir la promoción | Solo aparecen Mediana, Grande y Extragrande; la 2.ª pizza en $0 | | |
| PF-11 | Pizza de mostrador (R6) | Ticket con pizza de mostrador | — | Cambiar a domicilio | Pide quitarla; la categoría Mostrador desaparece | | |
| PF-12 | Domicilio sin cliente (R2) | Ticket con productos | Modalidad domicilio, sin cliente | Cobrar | Aviso "requiere un cliente registrado" | | |
| PF-13 | Stock de bebida (R7, trigger) | Manzanita 600 ml stock 2 | 2 Manzanitas | Cobrar y volver a Bebidas | Manzanita aparece AGOTADO y deshabilitada | | |
| PF-14 | Transacción multitabla (23) | — | Pizza con modificadores + bebida | Cobrar | Folio nuevo; en MySQL existen PEDIDO, DETALLE_PEDIDO y DETALLE_MODIFICADOR | | |
| PF-15 | Despacho con SP | Pedido a domicilio EN_PREPARACION | Repartidor 1 | Pedidos → Despachar | Estado EN_RUTA y repartidor asignado | | |
| PF-16 | Cancelación restaura stock | Pedido con bebidas | — | Pedidos → Cancelar | Estado CANCELADO y stock de bebidas recuperado | | |
| PF-17 | Ingrediente NO HAY (R8, trigger) | — | Marcar Piña NO HAY | Configurar pizza | Piña aparece "Agotado" y no se puede agregar | | |
| PF-18 | Corte de caja (24) | Pedidos del día | Rango "Hoy" | Reportes → Corte | Totales y lista de pedidos; doble clic abre el detalle | | |
| PF-19 | Persistencia (26) | Pedido creado | — | Cerrar la app, abrir, ir a Pedidos | El pedido sigue ahí con su estado | | |
| PF-21 | Sartén solo Grande/Extragrande | — | Tamaño Mediana | Armar pizza → Masa y orilla | Botón Sartén deshabilitado ("Solo Grande y Extragrande"); en Grande se habilita | | |
| PF-22 | Número de casa solo dígitos (25) | — | Escribir o pegar "12-B" | Clientes → Añadir | Solo queda "12"; al guardar con letras se pide anotarlas en Referencias | | |
| PF-23 | Actualizar desde el detalle | Pedido activo | — | Pedidos → Ver detalle → Marcar entregado | El diálogo se cierra y la lista se refresca sola con el nuevo estado | | |
| PF-24 | Nuevo producto (CRUD) | — | Pizza "Rhino BBQ", receta 4 ingredientes, precios Mediana/Grande | Inventario → Productos → Nuevo producto | Aparece en la lista y en Armar pizza → Sabor | | |
| PF-25 | Renombrar producto | Producto existente | Nombre nuevo | Inventario → Productos → Renombrar | El nombre cambia en el POS, la comanda y los reportes; un nombre repetido se rechaza | | |
| PF-26 | Calzone configurable | Script 05 ejecutado | Calzone Mediano, SIN 1, CON 1, queso extra | Nuevo pedido → Armar calzone | Tamaños Chico a Extra grande; sin mitades, masa ni orilla; ticket con "Mediano" y "Queso extra" | | |
| PF-20 | Responsivo | — | Ventana a 1024 × 700 y maximizada | Redimensionar | Barra lateral compacta, botones visibles, sin controles cortados | | |

---

> **Orden del configurador:** está en el constructor de `FrmConfigurarPizza` (lista `_pasos`).

### Calzone
Se arma igual que una pizza con el botón **Armar calzone**: tamaño (**Chico, Mediano, Grande, Extra grande**), sabor si hay más de un calzone, ingredientes SIN/CON con el mismo límite de incluidos y, al final, **solo queso extra**. No tiene mitad y mitad, no cambia de masa y no lleva orilla. Los tamaños reutilizan los registros Chica/Mediana/Grande/Extragrande de `TAMANO_PRESENTACION` (así funcionan las tarifas de ingrediente extra y de queso extra) y la app los muestra en masculino.

### Productos (administrador)
*Inventario → Productos y precios*: **Nuevo producto** (nombre, categoría, receta base, máximo de ingredientes incluidos y precio por tamaño; en bebidas también el stock inicial), **Editar producto** (nombre, receta y tamaños), **Renombrar**, **Cambiar precio** y **Activar / desactivar**. Todo se guarda en una transacción (`PRODUCTO` + `PRODUCTO_PRECIO` + `RECETA_PRODUCTO`). Un tamaño con ventas no se borra (para no romper el historial); se avisa.

## 6. Pendientes detectados para el equipo (fuera de frontend)

1. **`Docs/Diccionario_Datos.md` no corresponde al esquema real.** Describe tablas `puestos`, `empleados`, `categorias`, `tamanios`… que **no existen** en `01_esquema.sql` ni en el *Análisis* (12 tablas: EMPLEADO, CLIENTE, PEDIDO, PRODUCTO, TAMANO_PRESENTACION, PRODUCTO_PRECIO, INGREDIENTE, RECETA_PRODUCTO, PRECIO_INGREDIENTE_EXTRA, TARIFA_OPCION_FINAL, DETALLE_PEDIDO, DETALLE_MODIFICADOR). El Proyecto Final no acepta documentación contradictoria → **DBA debe regenerarlo**.
2. **Contraseñas en texto plano.** El *Análisis* dice "contraseña cifrada", pero `02_datos.sql` y `EmpleadoDAO.Autenticar` comparan texto plano. Sugerencia: guardar `SHA2('admin123', 256)` en los datos y comparar `contrasena = SHA2(@pwd, 256)` en `Autenticar` y `Guardar`.
3. **Pagos:** el boceto tiene "PAGOS", pero el esquema no tiene una tabla de pagos. La interfaz calcula el cambio y lo imprime en el ticket, pero el método de pago **no se guarda**. Si el equipo lo quiere persistir, DBA tendría que agregar una columna `metodo_pago` en PEDIDO.
4. **Carpetas de entrega:** faltan `Diagramas/`, `Pruebas/` (matriz con resultados) y `Video/` según la estructura sugerida del Proyecto Final.
5. **Datos de prueba vs. reglas nuevas:** `02_datos.sql` trae la tarifa `MASA_SARTEN` para los 8 tamaños (solo debería existir para Grande y Extragrande) y el cliente Diana Morales con número de casa `890-B` (ahora solo se aceptan dígitos). DBA puede dejar `MASA_SARTEN` solo para `id_tamano` 4 y 5, y cambiar `890-B` por `890` con "Interior B" en referencias. Backend puede rechazar también la sartén en otros tamaños dentro de `ValidarPedidoNegocio`.
6. **Acentos:** los scripts SQL ahora empiezan con `SET NAMES utf8mb4;`. Si la base se cargó antes con acentos dañados (`SELECT nombre FROM INGREDIENTE WHERE nombre LIKE '%Ã%';` devuelve filas), hay que volver a ejecutar los scripts 01 → 04.
7. **Calzone:** ejecutar `Sql/05_ajuste_calzone.sql` sobre la base actual (agrega los 4 tamaños del calzone y quita "Orden" si no tiene ventas). Los precios del calzone (95 / 115 / 145 / 175) son **de ejemplo**: el dueño debe confirmarlos y el admin ajustarlos en Inventario. `02_datos.sql` ya trae los nuevos precios para reconstrucciones desde cero.

