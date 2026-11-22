# Rhinos Pizza — Sistema POS y Administración

Sistema de punto de venta de escritorio para la pizzería **Rhinos Pizza**: pedidos telefónicos y de mostrador, configuración de pizzas (mitades, 2x1, ingredientes SIN/CON, fase final), despacho a repartidores, inventario de bebidas e ingredientes, y corte de caja.

Proyecto final de **Base de Datos** — UABC FIAD.
Equipo: Diego Nahum Nuñez Garcia · Daniel Alexandro Hernandez Melchor · Alejandro Palomares Ceseña.

---

## 1. Arquitectura

```
Usuarios (cajeras / administrador)
        │
        ▼
Interfaz WinForms (.NET 8)  ── Formularios/, Vistas/, Controles/
        │
        ▼
Capa de acceso a datos (DAO) ── Datos/  (consultas parametrizadas, transacciones)
        │   MySql.Data
        ▼
SGBD MySQL 8.0  ── vistas, triggers, función y procedimientos (Sql/)
        │
        ▼
Base de datos pizzeria_rhinos (12 tablas)
```

* La cadena de conexión **no está en el código**: se lee de *Secretos de usuario* (`secrets.json`).
* Todas las consultas usan parámetros (`@param`) para evitar inyección SQL.
* El registro de un pedido (PEDIDO + DETALLE_PEDIDO + DETALLE_MODIFICADOR) se hace en **una transacción** con `COMMIT` / `ROLLBACK`.
* El descuento de stock de bebidas lo hace **solo** el trigger `trg_validar_y_descontar_stock` (la aplicación no lo duplica).

## 2. Requisitos

| Componente | Versión |
| --- | --- |
| Windows | 10 u 11 |
| Visual Studio | 2022 17.13 o superior / Visual Studio 2026 (formato de solución `.slnx`) con la carga de trabajo *Desarrollo de escritorio de .NET* |
| SDK de .NET | 8.0 |
| MySQL Server | 8.0 (servicio `MySQL80` iniciado) |
| Impresora (opcional) | Impresora térmica de 80 mm o *Microsoft Print to PDF* para la vista previa de comandas y tickets |

## 3. Instalación de la base de datos

Ejecutar en MySQL Workbench, **en este orden**:

1. `Sql/01_esquema.sql` — crea la base `pizzeria_rhinos` y las 12 tablas con PK, FK, UNIQUE y CHECK.
2. `Sql/02_datos.sql` — datos de prueba (empleados, clientes, menú, precios, tarifas).
3. `Sql/03_consultas_vistas.sql` — vistas `vw_menu_pos_activo`, `vw_resumen_pedidos_corte` y las 10 consultas documentadas.
4. `Sql/04_rutinas_pruebas.sql` — 2 triggers, la función `fn_calcular_costo_extra_ingrediente` y los procedimientos `sp_crear_pedido_cabecera` y `sp_asignar_repartidor_pedido`.

## 4. Configuración de la conexión

1. Abrir `Aplicacion/POS_PizzeriaRhinos/POS_PizzeriaRhinos.slnx` en Visual Studio.
2. Clic derecho en el proyecto → **Administrar secretos de usuario**.
3. Pegar (cambiando la contraseña):

```json
{
  "ConnectionStrings": {
    "PizzeriaRhinosDB": "Server=localhost;Port=3306;Database=pizzeria_rhinos;Uid=root;Pwd=TU_CONTRASEÑA;"
  }
}
```

## 5. Ejecución

* **F5** en Visual Studio. La pantalla de inicio indica si hay conexión con MySQL.
* Usuarios de prueba:

| Usuario | Contraseña | Rol | Módulos |
| --- | --- | --- | --- |
| `admin` | `admin123` | ADMIN | Todos (incluye Inventario, Empleados y Reportes) |
| `cajera1` | `caja123` | CAJERO | Inicio, Nuevo pedido, Pedidos, Clientes |
| `cajera2` | `caja123` | CAJERO | Inicio, Nuevo pedido, Pedidos, Clientes |

Los repartidores no inician sesión (no tienen usuario); se asignan a los pedidos. La cocina no tiene acceso: recibe la **comanda impresa**.

## 6. Módulos de la aplicación

| Módulo | Qué hace | Tablas / objetos de BD |
| --- | --- | --- |
| **Inicio** | Indicadores del día y avisos (bebidas con stock bajo, ingredientes agotados) | PEDIDO, PRODUCTO_PRECIO, INGREDIENTE |
| **Nuevo pedido** | Modalidad, cliente por teléfono, menú en tarjetas, configurador de pizza, ticket, cobro e impresión | `vw_menu_pos_activo`, `fn_calcular_costo_extra_ingrediente`, transacción multitabla, `trg_validar_y_descontar_stock`, `trg_validar_modificador_ingrediente` |
| **Pedidos** | Seguimiento, despacho a ruta, entregado, cancelación (regresa stock), detalle y reimpresión | `vw_resumen_pedidos_corte`, `sp_asignar_repartidor_pedido` |
| **Clientes** | CRUD completo, búsqueda e historial de pedidos | CLIENTE, PEDIDO |
| **Inventario** (admin) | CRUD de ingredientes y HAY / NO HAY, stock de bebidas, precios y productos activos | INGREDIENTE, PRECIO_INGREDIENTE_EXTRA, PRODUCTO, PRODUCTO_PRECIO |
| **Empleados** (admin) | CRUD de cajeras, repartidores y administradores | EMPLEADO |
| **Reportes** (admin) | Corte de caja, liquidación de repartidores, ventas por cajera y modalidad, lo más vendido, exportar CSV | Consultas con JOIN, GROUP BY y agregados |

## 7. Estructura del código

```
Aplicacion/POS_PizzeriaRhinos/
├── Program.cs                 Ciclo de sesión (login → ventana principal → cerrar sesión)
├── Config/ConexionMySQL.cs    Lee la cadena de conexión de los secretos de usuario
├── Modelos/                   Cliente, Empleado, ItemCarrito, Sesion, modelos de reportes
├── Datos/                     DAOs: Cliente, Empleado, Pedido, Producto, Ingrediente, Reporte
├── Controles/                 Componentes visuales: botones grandes, tarjetas, tablas con estado vacío, teclados
├── Formularios/               Login, ventana principal, configurador de pizza, cobro, diálogos, formularios CRUD
├── Vistas/                    Módulos: Inicio, Nuevo pedido, Pedidos, Clientes, Inventario, Empleados, Reportes
├── Impresion/                 Comanda de cocina y ticket de 80 mm con vista previa
└── Recursos/                  Logo e ícono de Rhinos Pizza
```

Más detalle del frontend y la matriz de pruebas: [`Docs/GUIA_FRONTEND.md`](Docs/GUIA_FRONTEND.md).
