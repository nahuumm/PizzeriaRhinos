-- Fuerza UTF-8 en la sesión para que acentos y ñ se guarden bien (aunque se ejecute desde consola)
SET NAMES utf8mb4;

-- =========================================================================
-- PROYECTO FINAL: SISTEMA POS Y ADMINISTRACIÓN PARA PIZZERÍA (RHINOS)
-- ARCHIVO: 04_rutinas_pruebas.sql
-- DESCRIPCIÓN: 2 Triggers, 1 Función Almacenada y 2 Procedimientos Almacenados
-- =========================================================================

USE pizzeria_rhinos;

DELIMITER //

-- -------------------------------------------------------------------------
-- TRIGGER 1: Descuento automático de stock en bebidas y bloqueo si stock = 0
-- Evento: BEFORE INSERT ON DETALLE_PEDIDO
-- -------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_validar_y_descontar_stock //
CREATE TRIGGER trg_validar_y_descontar_stock
BEFORE INSERT ON DETALLE_PEDIDO
FOR EACH ROW
BEGIN
    DECLARE v_stock INT;
    DECLARE v_prod_disp BOOLEAN;

    SELECT pp.stock_actual, p.disponible
      INTO v_stock, v_prod_disp
      FROM PRODUCTO_PRECIO pp
      INNER JOIN PRODUCTO p ON pp.id_producto = p.id_producto
     WHERE pp.id_producto_precio = NEW.id_producto_precio
     FOR UPDATE;

    IF v_prod_disp = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Error de negocio: El producto seleccionado está desactivado por el administrador.';
    END IF;

    -- Si stock_actual no es NULL (es bebida), validar existencia y descontar
    IF v_stock IS NOT NULL THEN
        IF v_stock < NEW.cantidad THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Error de inventario: Stock insuficiente para la bebida solicitada.';
        END IF;

        UPDATE PRODUCTO_PRECIO
           SET stock_actual = stock_actual - NEW.cantidad
         WHERE id_producto_precio = NEW.id_producto_precio;
    END IF;
END //

-- -------------------------------------------------------------------------
-- TRIGGER 2: Validar disponibilidad de ingrediente y regla de negocio en modificadores
-- Evento: BEFORE INSERT ON DETALLE_MODIFICADOR
-- -------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_validar_modificador_ingrediente //
CREATE TRIGGER trg_validar_modificador_ingrediente
BEFORE INSERT ON DETALLE_MODIFICADOR
FOR EACH ROW
BEGIN
    DECLARE v_ing_disp BOOLEAN;

    SELECT disponible INTO v_ing_disp
      FROM INGREDIENTE
     WHERE id_ingrediente = NEW.id_ingrediente;

    IF v_ing_disp = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Error de negocio: El ingrediente seleccionado se encuentra agotado/desactivado.';
    END IF;
END //

-- -------------------------------------------------------------------------
-- FUNCIÓN ALMACENADA: Calcular costo de ingredientes extra (a partir del 6.°)
-- -------------------------------------------------------------------------
DROP FUNCTION IF EXISTS fn_calcular_costo_extra_ingrediente //
CREATE FUNCTION fn_calcular_costo_extra_ingrediente(
    p_id_producto INT,
    p_id_tamano INT,
    p_id_ingrediente_agregado INT,
    p_cantidad_agregados_previos INT
) RETURNS DECIMAL(10,2)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE v_base_count INT DEFAULT 0;
    DECLARE v_max_base INT DEFAULT 5;
    DECLARE v_total_actual INT DEFAULT 0;
    DECLARE v_tarifa DECIMAL(10,2) DEFAULT 0.00;

    SELECT COUNT(*) INTO v_base_count
      FROM RECETA_PRODUCTO
     WHERE id_producto = p_id_producto;

    SELECT max_ingredientes_base INTO v_max_base
      FROM PRODUCTO
     WHERE id_producto = p_id_producto;

    SET v_total_actual = v_base_count + p_cantidad_agregados_previos + 1;

    IF v_total_actual > v_max_base THEN
        SELECT COALESCE(precio_extra, 0.00) INTO v_tarifa
          FROM PRECIO_INGREDIENTE_EXTRA
         WHERE id_ingrediente = p_id_ingrediente_agregado
           AND id_tamano = p_id_tamano
         LIMIT 1;
        RETURN v_tarifa;
    ELSE
        RETURN 0.00;
    END IF;
END //

-- -------------------------------------------------------------------------
-- PROCEDIMIENTO ALMACENADO 1: Registrar Pedido con Transacción (COMMIT / ROLLBACK)
-- -------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_crear_pedido_cabecera //
CREATE PROCEDURE sp_crear_pedido_cabecera(
    IN p_id_cliente INT,
    IN p_id_cajero INT,
    IN p_id_repartidor INT,
    IN p_tipo_servicio VARCHAR(20),
    OUT p_id_pedido_generado INT
)
BEGIN
    DECLARE v_rol_cajero VARCHAR(20);
    DECLARE v_rol_repartidor VARCHAR(20);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    -- Validar que el cajero exista y tenga rol CAJERO o ADMIN
    SELECT rol INTO v_rol_cajero FROM EMPLEADO WHERE id_empleado = p_id_cajero AND activo = TRUE;
    IF v_rol_cajero IS NULL OR v_rol_cajero NOT IN ('CAJERO', 'ADMIN') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Error de seguridad: El empleado asignado como cajero no es válido o está inactivo.';
    END IF;

    -- Si es DOMICILIO, validar repartidor si viene asignado
    IF p_tipo_servicio = 'DOMICILIO' AND p_id_repartidor IS NOT NULL THEN
        SELECT rol INTO v_rol_repartidor FROM EMPLEADO WHERE id_empleado = p_id_repartidor AND activo = TRUE;
        IF v_rol_repartidor IS NULL OR v_rol_repartidor <> 'REPARTIDOR' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Error operativo: El empleado seleccionado no corresponde a un repartidor activo.';
        END IF;
    END IF;

    INSERT INTO PEDIDO (id_cliente, id_cajero, id_repartidor, fecha_hora, tipo_servicio, subtotal, total_extras, total, estado)
    VALUES (p_id_cliente, p_id_cajero, p_id_repartidor, NOW(), p_tipo_servicio, 0.00, 0.00, 0.00, 'EN_PREPARACION');

    SET p_id_pedido_generado = LAST_INSERT_ID();

    COMMIT;
END //

-- -------------------------------------------------------------------------
-- PROCEDIMIENTO ALMACENADO 2: Asignar Repartidor y despachar pedido ("En Ruta")
-- -------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_asignar_repartidor_pedido //
CREATE PROCEDURE sp_asignar_repartidor_pedido(
    IN p_id_pedido INT,
    IN p_id_repartidor INT
)
BEGIN
    DECLARE v_tipo_servicio VARCHAR(20);
    DECLARE v_rol_rep VARCHAR(20);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT tipo_servicio INTO v_tipo_servicio FROM PEDIDO WHERE id_pedido = p_id_pedido FOR UPDATE;
    IF v_tipo_servicio IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Error: El folio de pedido no existe.';
    END IF;

    IF v_tipo_servicio <> 'DOMICILIO' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Error: Solo los pedidos a DOMICILIO requieren repartidor.';
    END IF;

    SELECT rol INTO v_rol_rep FROM EMPLEADO WHERE id_empleado = p_id_repartidor AND activo = TRUE;
    IF v_rol_rep IS NULL OR v_rol_rep <> 'REPARTIDOR' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Error: Repartidor inválido o inactivo.';
    END IF;

    UPDATE PEDIDO
       SET id_repartidor = p_id_repartidor,
           estado = 'EN_RUTA'
     WHERE id_pedido = p_id_pedido;

    COMMIT;
END //

DELIMITER ;