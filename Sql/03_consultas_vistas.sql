-- Fuerza UTF-8 en la sesión para que acentos y ñ se guarden bien (aunque se ejecute desde consola)
SET NAMES utf8mb4;

-- =========================================================================
-- PROYECTO FINAL: SISTEMA POS Y ADMINISTRACIÓN PARA PIZZERÍA (RHINOS)
-- ARCHIVO: 03_consultas_vistas.sql
-- DESCRIPCIÓN: 2 Vistas funcionales + 10 Consultas SQL documentadas
-- =========================================================================

USE pizzeria_rhinos;

-- -------------------------------------------------------------------------
-- VISTA 1: Catálogo activo para el POS (productos, tamaños, precios y stock)
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_menu_pos_activo AS
SELECT 
    pp.id_producto_precio,
    p.id_producto,
    p.nombre AS producto,
    p.categoria,
    p.max_ingredientes_base,
    t.id_tamano,
    t.nombre AS tamano_presentacion,
    t.aplica_promocion_2x1,
    pp.variante,
    pp.precio,
    pp.stock_actual
FROM PRODUCTO_PRECIO pp
INNER JOIN PRODUCTO p ON pp.id_producto = p.id_producto
INNER JOIN TAMANO_PRESENTACION t ON pp.id_tamano = t.id_tamano
WHERE p.disponible = TRUE
  AND (pp.stock_actual IS NULL OR pp.stock_actual > 0);

-- -------------------------------------------------------------------------
-- VISTA 2: Resumen operativo de pedidos para Corte de Caja y Repartidores
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_resumen_pedidos_corte AS
SELECT 
    ped.id_pedido AS folio,
    ped.fecha_hora,
    ped.tipo_servicio,
    ped.estado,
    COALESCE(cli.nombre, 'Público General (Mostrador)') AS cliente,
    COALESCE(cli.telefono, 'N/A') AS telefono,
    caj.nombre AS cajero_responsable,
    COALESCE(rep.nombre, 'Sin asignar') AS repartidor_asignado,
    ped.subtotal,
    ped.total_extras,
    ped.total
FROM PEDIDO ped
INNER JOIN EMPLEADO caj ON ped.id_cajero = caj.id_empleado
LEFT JOIN CLIENTE cli ON ped.id_cliente = cli.id_cliente
LEFT JOIN EMPLEADO rep ON ped.id_repartidor = rep.id_empleado;

-- =========================================================================
-- 10 CONSULTAS SQL REQUERIDAS (Etapa 4 - Punto 17)
-- =========================================================================

-- 1. [JOIN] Consultar receta base de cada especialidad de pizza
SELECT p.nombre AS especialidad, i.nombre AS ingrediente_base
FROM PRODUCTO p
INNER JOIN RECETA_PRODUCTO rp ON p.id_producto = rp.id_producto
INNER JOIN INGREDIENTE i ON rp.id_ingrediente = i.id_ingrediente
WHERE p.categoria = 'PIZZA'
ORDER BY p.nombre, i.nombre;

-- 2. [JOIN] Historial detallado de pedidos de un cliente por teléfono
SELECT c.nombre AS cliente, p.id_pedido, p.fecha_hora, pr.nombre AS producto, dp.subtotal_linea, p.estado
FROM CLIENTE c
INNER JOIN PEDIDO p ON c.id_cliente = p.id_cliente
INNER JOIN DETALLE_PEDIDO dp ON p.id_pedido = dp.id_pedido
INNER JOIN PRODUCTO_PRECIO pp ON dp.id_producto_precio = pp.id_producto_precio
INNER JOIN PRODUCTO pr ON pp.id_producto = pr.id_producto
WHERE c.telefono = '6461112233';

-- 3. [JOIN] Consultar bebidas disponibles y su stock en refrigerador
SELECT pr.nombre AS bebida, tp.nombre AS presentacion, pp.precio, pp.stock_actual
FROM PRODUCTO pr
INNER JOIN PRODUCTO_PRECIO pp ON pr.id_producto = pp.id_producto
INNER JOIN TAMANO_PRESENTACION tp ON pp.id_tamano = tp.id_tamano
WHERE pr.categoria = 'BEBIDA'
ORDER BY pp.stock_actual ASC;

-- 4. [SUBCONSULTA EXISTS] Clientes que ya han realizado al menos un pedido
SELECT c.id_cliente, c.telefono, c.nombre, c.colonia
FROM CLIENTE c
WHERE EXISTS (
    SELECT 1 FROM PEDIDO p WHERE p.id_cliente = c.id_cliente
);

-- 5. [SUBCONSULTA NOT EXISTS] Ingredientes que actualmente no forman parte de ninguna receta base
SELECT i.id_ingrediente, i.nombre
FROM INGREDIENTE i
WHERE NOT EXISTS (
    SELECT 1 FROM RECETA_PRODUCTO rp WHERE rp.id_ingrediente = i.id_ingrediente
);

-- 6. [AGREGACIÓN + GROUP BY] Total de ventas y número de pedidos por modalidad de servicio
SELECT tipo_servicio, COUNT(*) AS total_pedidos, SUM(total) AS ingreso_total
FROM PEDIDO
WHERE estado <> 'CANCELADO'
GROUP BY tipo_servicio;

-- 7. [AGREGACIÓN + GROUP BY + HAVING] Cajeras con ventas acumuladas mayores a $100.00
SELECT e.nombre AS cajera, COUNT(p.id_pedido) AS ordenes_capturadas, SUM(p.total) AS monto_vendido
FROM EMPLEADO e
INNER JOIN PEDIDO p ON e.id_empleado = p.id_cajero
WHERE e.rol = 'CAJERO' AND p.estado <> 'CANCELADO'
GROUP BY e.id_empleado, e.nombre
HAVING SUM(p.total) > 100.00;

-- 8. [AGREGACIÓN + GROUP BY + HAVING] Especialidades con exactamente 5 ingredientes base (al límite)
SELECT p.nombre AS especialidad, COUNT(rp.id_ingrediente) AS total_ingredientes_base
FROM PRODUCTO p
INNER JOIN RECETA_PRODUCTO rp ON p.id_producto = rp.id_producto
GROUP BY p.id_producto, p.nombre
HAVING COUNT(rp.id_ingrediente) = 5;

-- 9. [CONSULTA COMPLEJA] Tarifas de opciones finales ordenadas por tamaño
SELECT tp.nombre AS tamano, tof.tipo_opcion, tof.precio_adicional
FROM TARIFA_OPCION_FINAL tof
INNER JOIN TAMANO_PRESENTACION tp ON tof.id_tamano = tp.id_tamano
ORDER BY tp.id_tamano, tof.tipo_opcion;

-- 10. [CONSULTA COMPLEJA] Liquidación de entregas por repartidor
SELECT e.nombre AS repartidor, COUNT(p.id_pedido) AS entregas_realizadas, COALESCE(SUM(p.total), 0) AS efectivo_a_liquidar
FROM EMPLEADO e
LEFT JOIN PEDIDO p ON e.id_empleado = p.id_repartidor AND p.estado = 'ENTREGADO'
WHERE e.rol = 'REPARTIDOR'
GROUP BY e.id_empleado, e.nombre;