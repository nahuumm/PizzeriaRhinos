-- Fuerza UTF-8 en la sesión para que acentos y ñ se guarden bien (aunque se ejecute desde consola)
SET NAMES utf8mb4;

-- =========================================================================
-- PROYECTO FINAL: SISTEMA POS Y ADMINISTRACIÓN PARA PIZZERÍA (RHINOS)
-- ARCHIVO: 01_esquema.sql
-- SGBD: MySQL 8.0
-- =========================================================================

DROP DATABASE IF EXISTS pizzeria_rhinos;
CREATE DATABASE pizzeria_rhinos
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE pizzeria_rhinos;

-- 1. EMPLEADO (Solo Cajeras, Repartidores y Admin; sin cuentas para cocineros)
CREATE TABLE EMPLEADO (
    id_empleado INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    rol VARCHAR(20) NOT NULL,
    usuario VARCHAR(30) UNIQUE NULL,
    contrasena VARCHAR(255) NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT chk_empleado_rol CHECK (rol IN ('CAJERO', 'REPARTIDOR', 'ADMIN'))
) ENGINE=InnoDB;

-- 2. CLIENTE (Búsqueda única por teléfono)
CREATE TABLE CLIENTE (
    id_cliente INT AUTO_INCREMENT PRIMARY KEY,
    telefono VARCHAR(15) NOT NULL UNIQUE,
    nombre VARCHAR(100) NOT NULL,
    calle VARCHAR(100) NOT NULL,
    numero_casa VARCHAR(20) NOT NULL,
    colonia VARCHAR(80) NOT NULL,
    entre_calles VARCHAR(120) NULL,
    referencias TEXT NULL
) ENGINE=InnoDB;

-- 3. PEDIDO (Cabecera transaccional)
CREATE TABLE PEDIDO (
    id_pedido INT AUTO_INCREMENT PRIMARY KEY,
    id_cliente INT NULL,
    id_cajero INT NOT NULL,
    id_repartidor INT NULL,
    fecha_hora DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    tipo_servicio VARCHAR(20) NOT NULL,
    subtotal DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    total_extras DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    total DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    estado VARCHAR(25) NOT NULL DEFAULT 'EN_PREPARACION',
    CONSTRAINT fk_pedido_cliente FOREIGN KEY (id_cliente) REFERENCES CLIENTE(id_cliente)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_pedido_cajero FOREIGN KEY (id_cajero) REFERENCES EMPLEADO(id_empleado)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_pedido_repartidor FOREIGN KEY (id_repartidor) REFERENCES EMPLEADO(id_empleado)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT chk_pedido_servicio CHECK (tipo_servicio IN ('DOMICILIO', 'MOSTRADOR')),
    CONSTRAINT chk_pedido_estado CHECK (estado IN ('EN_PREPARACION', 'EN_RUTA', 'ENTREGADO', 'CANCELADO')),
    CONSTRAINT chk_pedido_montos CHECK (subtotal >= 0 AND total_extras >= 0 AND total >= 0)
) ENGINE=InnoDB;

-- 4. PRODUCTO (Catálogo maestro)
CREATE TABLE PRODUCTO (
    id_producto INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(80) NOT NULL,
    categoria VARCHAR(30) NOT NULL,
    max_ingredientes_base INT NOT NULL DEFAULT 5,
    disponible BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT chk_producto_categoria CHECK (categoria IN ('PIZZA', 'PIZZA_MOSTRADOR', 'CALZONE', 'COMPLEMENTO', 'BEBIDA')),
    CONSTRAINT chk_producto_max_ing CHECK (max_ingredientes_base >= 0)
) ENGINE=InnoDB;

-- 5. TAMANO_PRESENTACION (Tamaños y regla 2x1 para Mediana, Grande y Extragrande)
CREATE TABLE TAMANO_PRESENTACION (
    id_tamano INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL UNIQUE,
    aplica_promocion_2x1 BOOLEAN NOT NULL DEFAULT FALSE
) ENGINE=InnoDB;

-- 6. PRODUCTO_PRECIO (Tarifas por tamaño/variante y stock automático para bebidas)
CREATE TABLE PRODUCTO_PRECIO (
    id_producto_precio INT AUTO_INCREMENT PRIMARY KEY,
    id_producto INT NOT NULL,
    id_tamano INT NOT NULL,
    variante VARCHAR(30) NOT NULL DEFAULT 'NORMAL',
    precio DECIMAL(10,2) NOT NULL,
    stock_actual INT NULL,
    CONSTRAINT fk_pp_producto FOREIGN KEY (id_producto) REFERENCES PRODUCTO(id_producto)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_pp_tamano FOREIGN KEY (id_tamano) REFERENCES TAMANO_PRESENTACION(id_tamano)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT uq_producto_tamano_variante UNIQUE (id_producto, id_tamano, variante),
    CONSTRAINT chk_pp_variante CHECK (variante IN ('NORMAL', 'SARTEN', 'MOSTRADOR')),
    CONSTRAINT chk_pp_precio CHECK (precio >= 0),
    CONSTRAINT chk_pp_stock CHECK (stock_actual IS NULL OR stock_actual >= 0)
) ENGINE=InnoDB;

-- 7. INGREDIENTE (Control manual del dueño sin gramaje)
CREATE TABLE INGREDIENTE (
    id_ingrediente INT AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(60) NOT NULL UNIQUE,
    disponible BOOLEAN NOT NULL DEFAULT TRUE
) ENGINE=InnoDB;

-- 8. RECETA_PRODUCTO (Ingredientes incluidos por defecto en cada especialidad)
CREATE TABLE RECETA_PRODUCTO (
    id_producto INT NOT NULL,
    id_ingrediente INT NOT NULL,
    PRIMARY KEY (id_producto, id_ingrediente),
    CONSTRAINT fk_receta_producto FOREIGN KEY (id_producto) REFERENCES PRODUCTO(id_producto)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_receta_ingrediente FOREIGN KEY (id_ingrediente) REFERENCES INGREDIENTE(id_ingrediente)
        ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB;

-- 9. PRECIO_INGREDIENTE_EXTRA (Costo a partir del 6.° ingrediente según tamaño)
CREATE TABLE PRECIO_INGREDIENTE_EXTRA (
    id_precio_extra INT AUTO_INCREMENT PRIMARY KEY,
    id_ingrediente INT NOT NULL,
    id_tamano INT NOT NULL,
    precio_extra DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_pie_ingrediente FOREIGN KEY (id_ingrediente) REFERENCES INGREDIENTE(id_ingrediente)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_pie_tamano FOREIGN KEY (id_tamano) REFERENCES TAMANO_PRESENTACION(id_tamano)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT uq_ingrediente_tamano_extra UNIQUE (id_ingrediente, id_tamano),
    CONSTRAINT chk_pie_precio CHECK (precio_extra >= 0)
) ENGINE=InnoDB;

-- 10. TARIFA_OPCION_FINAL (Orilla de queso, queso extra, masa de sartén por tamaño)
CREATE TABLE TARIFA_OPCION_FINAL (
    id_tarifa_opcion INT AUTO_INCREMENT PRIMARY KEY,
    id_tamano INT NOT NULL,
    tipo_opcion VARCHAR(30) NOT NULL,
    precio_adicional DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_tof_tamano FOREIGN KEY (id_tamano) REFERENCES TAMANO_PRESENTACION(id_tamano)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT uq_tamano_tipo_opcion UNIQUE (id_tamano, tipo_opcion),
    CONSTRAINT chk_tof_opcion CHECK (tipo_opcion IN ('ORILLA_QUESO', 'QUESO_EXTRA', 'MASA_SARTEN')),
    CONSTRAINT chk_tof_precio CHECK (precio_adicional >= 0)
) ENGINE=InnoDB;

-- 11. DETALLE_PEDIDO (Partidas, mitades y emparejamiento 2x1)
CREATE TABLE DETALLE_PEDIDO (
    id_detalle INT AUTO_INCREMENT PRIMARY KEY,
    id_pedido INT NOT NULL,
    id_producto_precio INT NOT NULL,
    id_producto_mitad2 INT NULL,
    cantidad INT NOT NULL DEFAULT 1,
    tipo_masa VARCHAR(20) NULL DEFAULT 'GRUESA',
    con_orilla_queso BOOLEAN NOT NULL DEFAULT FALSE,
    con_queso_extra BOOLEAN NOT NULL DEFAULT FALSE,
    es_promocion_2x1 BOOLEAN NOT NULL DEFAULT FALSE,
    grupo_promo INT NULL,
    subtotal_linea DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_dp_pedido FOREIGN KEY (id_pedido) REFERENCES PEDIDO(id_pedido)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_dp_producto_precio FOREIGN KEY (id_producto_precio) REFERENCES PRODUCTO_PRECIO(id_producto_precio)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_dp_mitad2 FOREIGN KEY (id_producto_mitad2) REFERENCES PRODUCTO(id_producto)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT chk_dp_cantidad CHECK (cantidad > 0),
    CONSTRAINT chk_dp_masa CHECK (tipo_masa IS NULL OR tipo_masa IN ('GRUESA', 'DELGADA', 'SARTEN')),
    CONSTRAINT chk_dp_subtotal CHECK (subtotal_linea >= 0)
) ENGINE=InnoDB;

-- 12. DETALLE_MODIFICADOR (Instrucciones SIN / CON por partida o mitad)
CREATE TABLE DETALLE_MODIFICADOR (
    id_modificador INT AUTO_INCREMENT PRIMARY KEY,
    id_detalle INT NOT NULL,
    id_ingrediente INT NOT NULL,
    tipo_accion VARCHAR(10) NOT NULL,
    mitad_aplicada VARCHAR(15) NOT NULL DEFAULT 'COMPLETA',
    costo_aplicado DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT fk_dm_detalle FOREIGN KEY (id_detalle) REFERENCES DETALLE_PEDIDO(id_detalle)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_dm_ingrediente FOREIGN KEY (id_ingrediente) REFERENCES INGREDIENTE(id_ingrediente)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT chk_dm_accion CHECK (tipo_accion IN ('SIN', 'CON')),
    CONSTRAINT chk_dm_mitad CHECK (mitad_aplicada IN ('COMPLETA', 'MITAD_1', 'MITAD_2')),
    CONSTRAINT chk_dm_costo CHECK (costo_aplicado >= 0)
) ENGINE=InnoDB;