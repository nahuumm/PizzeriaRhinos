# Diccionario de Datos — Sistema Pizzeria Rhinos (`pizzeria_rhinos`)

Este documento contiene la especificación detallada del modelo relacional de la base de datos `pizzeria_rhinos`, compuesto por 12 tablas.

---

## 1. Tabla: `puestos`
Almacena los roles y cargos de los empleados dentro de la pizzería.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_puesto` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del puesto |
| `nombre` | `VARCHAR(50)` | NO | UNIQUE | | Nombre del puesto (ej. Cajero, Pizzero, Repartidor) |
| `salario_base` | `DECIMAL(10,2)` | NO | | `CHECK (salario_base > 0)` | Salario base asignado al puesto |

---

## 2. Tabla: `empleados`
Registra el personal que labora en la sucursal y opera el sistema POS.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_empleado` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del empleado |
| `nombre` | `VARCHAR(50)` | NO | | | Nombre(s) del empleado |
| `apellido` | `VARCHAR(50)` | NO | | | Apellido(s) del empleado |
| `telefono` | `VARCHAR(15)` | SI | | | Teléfono de contacto |
| `email` | `VARCHAR(100)` | NO | UNIQUE | | Correo electrónico institucional |
| `id_puesto` | `INT` | NO | FK | `REFERENCES puestos(id_puesto)` | Puesto asignado |
| `activo` | `TINYINT(1)` | NO | | `DEFAULT 1` | Estado laboral (1 = Activo, 0 = Inactivo) |
| `fecha_contratacion` | `DATE` | NO | | | Fecha de ingreso a la empresa |

---

## 3. Tabla: `clientes`
Almacena la información de los clientes para la toma de pedidos.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_cliente` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del cliente |
| `nombre` | `VARCHAR(50)` | NO | | | Nombre del cliente |
| `apellido` | `VARCHAR(50)` | NO | | | Apellido del cliente |
| `telefono` | `VARCHAR(15)` | NO | UNIQUE | | Teléfono principal de contacto |
| `direccion` | `VARCHAR(200)` | SI | | | Dirección de entrega |
| `email` | `VARCHAR(100)` | SI | UNIQUE | | Correo electrónico del cliente |
| `fecha_registro` | `DATETIME` | NO | | `DEFAULT CURRENT_TIMESTAMP` | Fecha y hora de alta en el sistema |

---

## 4. Tabla: `categorias`
Clasifica los productos ofrecidos en el menú (Pizzas, Bebidas, Entradas, Postres).

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_categoria` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único de la categoría |
| `nombre` | `VARCHAR(50)` | NO | UNIQUE | | Nombre de la categoría |
| `descripcion` | `VARCHAR(150)` | SI | | | Descripción breve de la categoría |

---

## 5. Tabla: `productos`
Catálogo general de productos del menú.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_producto` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del producto |
| `nombre` | `VARCHAR(80)` | NO | UNIQUE | | Nombre del producto |
| `descripcion` | `TEXT` | SI | | | Descripción comercial del producto |
| `precio_base` | `DECIMAL(10,2)` | NO | | `CHECK (precio_base >= 0)` | Precio base antes de modificaciones |
| `id_categoria` | `INT` | NO | FK | `REFERENCES categorias(id_categoria)` | Categoría del producto |
| `disponible` | `TINYINT(1)` | NO | | `DEFAULT 1` | Indica disponibilidad en menú |

---

## 6. Tabla: `tamanios`
Define los tamaños configurables para las pizzas y bebidas.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_tamanio` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del tamaño |
| `nombre` | `VARCHAR(30)` | NO | UNIQUE | | Nombre (Individual, Mediana, Familiar, Jumbo) |
| `multiplicador_precio` | `DECIMAL(4,2)` | NO | | `CHECK (multiplicador_precio > 0)` | Factor multiplicador aplicado al precio base |

---

## 7. Tabla: `ingredientes`
Control de inventario de materias primas e ingredientes de la pizzería.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_ingrediente` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del ingrediente |
| `nombre` | `VARCHAR(60)` | NO | UNIQUE | | Nombre del ingrediente |
| `costo_extra` | `DECIMAL(10,2)` | NO | | `CHECK (costo_extra >= 0)` | Costo adicional por porción extra |
| `stock_actual` | `INT` | NO | | `CHECK (stock_actual >= 0)` | Cantidad disponible en almacén |
| `stock_minimo` | `INT` | NO | | `CHECK (stock_minimo >= 0)` | Umbral mínimo para alerta de reabastecimiento |
| `unidad_medida` | `VARCHAR(20)` | NO | | | Unidad de medida (gr, piezas, ml) |

---

## 8. Tabla: `producto_ingredientes`
Relación de receta base entre un producto y sus ingredientes por defecto.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_producto` | `INT` | NO | PK, FK | `REFERENCES productos(id_producto)` | Producto de la receta |
| `id_ingrediente` | `INT` | NO | PK, FK | `REFERENCES ingredientes(id_ingrediente)` | Ingrediente necesario |
| `cantidad_requerida` | `INT` | NO | | `CHECK (cantidad_requerida > 0)` | Cantidad de porciones usadas |

---

## 9. Tabla: `estatus_pedido`
Estados por los que transita una orden (ej. Pendiente, En Preparación, Entregado, Cancelado).

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_estatus` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del estatus |
| `nombre_estatus` | `VARCHAR(30)` | NO | UNIQUE | | Descripción del estado |

---

## 10. Tabla: `pedidos`
Encabezado principal de la orden de compra realizada.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_pedido` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador único del pedido |
| `id_cliente` | `INT` | NO | FK | `REFERENCES clientes(id_cliente)` | Cliente que solicitó la orden |
| `id_empleado` | `INT` | NO | FK | `REFERENCES empleados(id_empleado)` | Empleado que registró la orden |
| `id_estatus` | `INT` | NO | FK | `REFERENCES estatus_pedido(id_estatus)` | Estado actual de la orden |
| `fecha_pedido` | `DATETIME` | NO | | `DEFAULT CURRENT_TIMESTAMP` | Fecha y hora de creación |
| `metodo_pago` | `VARCHAR(30)` | NO | | `CHECK (metodo_pago IN ('Efectivo', 'Tarjeta', 'Transferencia'))` | Forma de pago |
| `total` | `DECIMAL(10,2)` | NO | | `DEFAULT 0.00 CHECK (total >= 0)` | Monto total acumulado del pedido |

---

## 11. Tabla: `detalle_pedidos`
Líneas de detalle asociadas a cada producto dentro de un pedido.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_detalle` | `INT` | NO | PK | `AUTO_INCREMENT` | Identificador de la línea de detalle |
| `id_pedido` | `INT` | NO | FK | `REFERENCES pedidos(id_pedido) ON DELETE CASCADE` | Pedido al que pertenece |
| `id_producto` | `INT` | NO | FK | `REFERENCES productos(id_producto)` | Producto seleccionado |
| `id_tamanio` | `INT` | NO | FK | `REFERENCES tamanios(id_tamanio)` | Tamaño elegido |
| `cantidad` | `INT` | NO | | `CHECK (cantidad > 0)` | Cantidad de unidades solicitadas |
| `precio_unitario` | `DECIMAL(10,2)` | NO | | `CHECK (precio_unitario >= 0)` | Precio unitario calculado |
| `subtotal` | `DECIMAL(10,2)` | NO | | `CHECK (subtotal >= 0)` | Subtotal de la línea (`cantidad` $\times$ `precio_unitario`) |

---

## 12. Tabla: `detalle_pedido_ingredientes`
Registro de ingredientes adicionales agregados a una pizza individual en el detalle del pedido.

| Columna | Tipo de Dato | Nulo | Llave | Restricción / Valor por Defecto | Descripción |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `id_detalle` | `INT` | NO | PK, FK | `REFERENCES detalle_pedidos(id_detalle) ON DELETE CASCADE` | Línea de detalle asociada |
| `id_ingrediente` | `INT` | NO | PK, FK | `REFERENCES ingredientes(id_ingrediente)` | Ingrediente extra agregado |
| `num_posicion` | `INT` | NO | | `CHECK (num_posicion >= 1)` | Número correlativo del ingrediente (1, 2, ..., N) |
| `costo_aplicado` | `DECIMAL(10,2)` | NO | | `DEFAULT 0.00` | Costo asignado según la regla del 6.º ingrediente |