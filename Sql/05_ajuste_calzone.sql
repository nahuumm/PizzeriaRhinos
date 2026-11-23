-- Fuerza UTF-8 en la sesión para que acentos y ñ se guarden bien (aunque se ejecute desde consola)
SET NAMES utf8mb4;

-- =========================================================================
-- 05_ajuste_calzone.sql
-- Calzone configurable como pizza: tamaños Chico, Mediano, Grande y Extra grande.
-- Usa los registros existentes de TAMANO_PRESENTACION (Chica, Mediana, Grande, Extragrande);
-- la aplicación los muestra en masculino para el calzone.
-- Ejecutar UNA vez sobre una base ya cargada (01 → 04). Si se reconstruye desde cero,
-- 02_datos.sql ya incluye estos precios y este script no hace falta (es seguro repetirlo).
-- No modifica el esquema: solo datos.
-- =========================================================================
USE pizzeria_rhinos;

-- Precios por tamaño del calzone (precios de ejemplo: ajustarlos en Inventario → Productos y precios)
INSERT IGNORE INTO PRODUCTO_PRECIO (id_producto, id_tamano, variante, precio, stock_actual)
SELECT p.id_producto, t.id_tamano, 'NORMAL',
       CASE UPPER(t.nombre) WHEN 'CHICA' THEN 95.00 WHEN 'MEDIANA' THEN 115.00 WHEN 'GRANDE' THEN 145.00 ELSE 175.00 END,
       NULL
FROM PRODUCTO p
CROSS JOIN TAMANO_PRESENTACION t
WHERE p.categoria = 'CALZONE'
  AND UPPER(t.nombre) IN ('CHICA', 'MEDIANA', 'GRANDE', 'EXTRAGRANDE');

-- Quita la presentación "Orden" del calzone solo si nunca se ha vendido (si tiene ventas se conserva).
-- El modo seguro de Workbench bloquea DELETE con JOIN; se desactiva solo para esta instrucción.
SET SQL_SAFE_UPDATES = 0;
DELETE pp FROM PRODUCTO_PRECIO pp
INNER JOIN PRODUCTO p ON p.id_producto = pp.id_producto
INNER JOIN TAMANO_PRESENTACION t ON t.id_tamano = pp.id_tamano
LEFT JOIN DETALLE_PEDIDO dp ON dp.id_producto_precio = pp.id_producto_precio
WHERE p.categoria = 'CALZONE' AND UPPER(t.nombre) = 'ORDEN' AND dp.id_detalle IS NULL;
SET SQL_SAFE_UPDATES = 1;

-- Verificación
SELECT p.nombre AS Calzone, t.nombre AS Tamano, pp.precio AS Precio
FROM PRODUCTO_PRECIO pp
INNER JOIN PRODUCTO p ON p.id_producto = pp.id_producto
INNER JOIN TAMANO_PRESENTACION t ON t.id_tamano = pp.id_tamano
WHERE p.categoria = 'CALZONE'
ORDER BY p.nombre, t.id_tamano;
