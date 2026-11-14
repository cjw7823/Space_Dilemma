#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Import the FULL, uncropped footprint of a moon DEM GeoTIFF into tiled Unity terrain.

The output represents every source elevation pixel's spatial footprint; the
original measurement interval remains in metres, though the samples are
bilinearly re-gridded to fit Unity's (2^n+1) per-tile heightmap constraint.
Uses source pixel-centre bounds, without inventing area beyond the source.
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
import rasterio
from scipy.ndimage import map_coordinates

SUPPORTED_RESOLUTIONS = (33, 65, 129, 257, 513, 1025, 2049, 4097)


def examine(src, resolution):
    dx = float(src.transform.a)
    dz = -float(src.transform.e)
    if abs(src.transform.b) > 1e-9 or abs(src.transform.d) > 1e-9 or dx <= 0 or dz <= 0:
        raise ValueError('DEM must be north-up, east-right and unrotated; reproject with GDAL/QGIS.')
    if src.crs is None:
        raise ValueError('GeoTIFF is missing its projected coordinate reference system (CRS).')
    if src.crs.is_geographic:
        raise ValueError('GeoTIFF units are degrees, not metres. Reproject to lunar polar stereographic CRS first.')
    if src.width <= 1 or src.height <= 1:
        raise ValueError('Source raster too small.')
    intervals = resolution - 1
    cols = math.ceil((src.width - 1) / intervals)
    rows = math.ceil((src.height - 1) / intervals)
    total_width = (src.width - 1) * dx
    total_length = (src.height - 1) * dz
    return dict(
        width=src.width, height=src.height, dx=dx, dz=dz,
        cols=cols, rows=rows,
        total_width=total_width, total_length=total_length,
        tile_width=total_width / cols, tile_length=total_length / rows,
        sample_step_x=total_width / (cols * intervals),
        sample_step_z=total_length / (rows * intervals),
        crs=str(src.crs),
    )


def print_info(info, filename, n):
    print(f'GeoTIFF: {filename}')
    print(f'Raster: {info["width"]:,} columns x {info["height"]:,} rows')
    print(f'Native grid spacing: {info["dx"]:g}m x {info["dz"]:g}m')
    print(f'Pixel-centre footprint: {info["total_width"] / 1000:.5f} x {info["total_length"] / 1000:.5f} km')
    print(f'Unity: {info["cols"]} columns x {info["rows"]} rows = {info["cols"] * info["rows"]} tiles at {n}x{n}')
    print(f'Tile size: {info["tile_width"]:.4f}m (X) x {info["tile_length"]:.4f}m (Z)')
    print(f'Effective Unity grid: {info["sample_step_x"]:.5f}m x {info["sample_step_z"]:.5f}m')
    print('NOTE: Grid resampling does not add new NASA measurements.')


def process(input_file: Path, output_folder: Path, n: int, nodata: str, force: bool, dry_run: bool):
    with rasterio.open(input_file) as src:
        if src.count != 1:
            raise ValueError(f'Expected one elevation band; source has {src.count}.')
        info = examine(src, n)
        print_info(info, input_file.name, n)
        if dry_run:
            return info
        manifest_path = output_folder / 'manifest.json'
        if manifest_path.exists() and not force:
            raise FileExistsError(f'{manifest_path} exists. Use a new output directory or --force to overwrite.')

        # Full raster read is manageable for a ~20x20 km 5m/pix DEM (~16M float samples).
        raster = src.read(1, masked=True)
        heights = np.array(raster.data, dtype=np.float32, copy=True)
        invalid = np.ma.getmaskarray(raster) | ~np.isfinite(heights) | (np.abs(heights) > 1e7)
        valid_count = int(np.count_nonzero(~invalid))
        if not valid_count:
            raise ValueError('No valid elevation samples found.')
        invalid_count = int(invalid.size - valid_count)
        if invalid_count:
            share = invalid_count / invalid.size * 100
            print(f'WARNING: {invalid_count:,} NoData pixels ({share:.2f}%).')
            if nodata == 'error':
                raise ValueError('Entire raster contains NoData. To fill gaps using nearest VALID height, explicitly use --nodata nearest. Filled regions are not observations.')
            from scipy.ndimage import distance_transform_edt
            nearest = distance_transform_edt(invalid, return_distances=False, return_indices=True)
            heights[invalid] = heights[tuple(nearest[:, invalid])]
            del nearest
            print('WARNING: NoData regions filled with nearest measured/estimated values. Treat these areas as NOT scientifically accurate.')

        valid_min = float(heights.min())
        valid_max = float(heights.max())
        vertical_size = valid_max - valid_min
        if vertical_size <= 0:
            vertical_size = 1.0
        output_folder.mkdir(parents=True, exist_ok=True)
        step = n - 1
        full_x_scale = (info['width'] - 1) / (info['cols'] * step)
        full_y_scale = (info['height'] - 1) / (info['rows'] * step)
        # Coordinates address original sample centres (not outer pixel edges).
        for r in range(info['rows']):
            yy = (r * step + np.arange(n, dtype=np.float64)) * full_y_scale
            for c in range(info['cols']):
                xx = (c * step + np.arange(n, dtype=np.float64)) * full_x_scale
                yy_grid, xx_grid = np.meshgrid(yy, xx, indexing='ij')
                tile = map_coordinates(
                    heights, [yy_grid, xx_grid], order=1, mode='nearest', prefilter=False,
                )
                quantized = np.rint((tile.astype(np.float64) - valid_min) * (65535.0 / vertical_size))
                quantized = np.clip(quantized, 0, 65535).astype('<u2')
                # Unity's SetHeights: first row = south / minimum Z.
                raw = np.flipud(quantized).copy()
                name = f'moon_r{r:02d}_c{c:02d}.raw'
                (output_folder / name).write_bytes(raw.tobytes(order='C'))
                print(f'[{r * info["cols"] + c + 1}/{info["rows"] * info["cols"]}] {name} ({raw.nbytes / 1048576:.2f} MiB)')

        # GEOREF: raster pixel 0,0 is at the NW centre. Unity world southwest is (0,0).
        northwest = src.transform * (0.5, 0.5)
        southeast = src.transform * (src.width - 0.5, src.height - 0.5)
        manifest = {
            'manifestVersion': 2,
            'tileCols': info['cols'], 'tileRows': info['rows'], 'tileResolution': n,
            'tileWidthMeters': info['tile_width'], 'tileLengthMeters': info['tile_length'],
            'totalWidthMeters': info['total_width'], 'totalLengthMeters': info['total_length'],
            'heightRangeMeters': vertical_size,
            'sourceMinHeightMeters': valid_min, 'sourceMaxHeightMeters': valid_max,
            'sourceWidthPixels': info['width'], 'sourceHeightPixels': info['height'],
            'sourcePixelWidthMeters': info['dx'], 'sourcePixelHeightMeters': info['dz'],
            'actualUnitySampleStepXMeters': info['sample_step_x'],
            'actualUnitySampleStepZMeters': info['sample_step_z'],
            'sourceCrs': info['crs'], 'sourceNorthwestPixelCentreXY': list(northwest),
            'sourceSoutheastPixelCentreXY': list(southeast),
            'filledNodataPixelCount': invalid_count,
            'sourceDataName': input_file.name,
            'notes': 'Physical dimensions match the source pixel-centre envelope. Heights are linearly resampled into 2^n+1 Unity grids; generated samples are not new NASA observations.',
        }
        manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding='utf-8')
        print(f'Saved {manifest_path}')
        print(f'Complete. Map size {info["total_width"] / 1000:.5f} x {info["total_length"] / 1000:.5f} km; Y range {vertical_size:.3f}m.')
        return info


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--input', type=Path, required=True, help='A307_GLDELEV_005.tif')
    p.add_argument('--output', type=Path, default=Path('Assets/MoonTerrain/InputFull'))
    p.add_argument('--resolution', type=int, choices=SUPPORTED_RESOLUTIONS, default=1025,
                   help='Samples per tile side. Default 1025.')
    p.add_argument('--nodata', choices=['error', 'nearest'], default='error',
                   help='Handle missing raster samples. nearest explicitly approximates missing elevations.')
    p.add_argument('--force', action='store_true', help='Overwrite a previous output manifest/RAW files.')
    p.add_argument('--dry-run', action='store_true', help='Only inspect raster size, map extent and planned tile count.')
    args = p.parse_args()
    process(args.input, args.output, args.resolution, args.nodata, args.force, args.dry_run)


if __name__ == '__main__':
    main()
