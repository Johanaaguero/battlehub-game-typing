/* eslint-disable @typescript-eslint/no-var-requires */
const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const { ModuleFederationPlugin } = require('webpack').container;
const { BundleAnalyzerPlugin } = require('webpack-bundle-analyzer');
const Dotenv = require('dotenv-webpack');
const TerserPlugin = require('terser-webpack-plugin');
// ADR-003 §3: dependencias compartidas, idénticas en el Shell y en los tres juegos.
const sharedDeps = require('./mf-shared');

// ADR-003 §2: nombre del remote y puerto local del Equipo 4.
const REMOTE_NAME = 'typingGame';
const PORT = 4001;

const cssLoader = {
    loader: 'css-loader'
};

const postcssLoader = {
    loader: 'postcss-loader',
    options: {
        postcssOptions: {
            plugins: [
                'autoprefixer'
            ]
        }
    }
};

module.exports = function (env, { analyze }) {
    const production = env.production || process.env.NODE_ENV === 'production';

    return {
        target: 'web',
        mode: production ? 'production' : 'development',
        devtool: production ? undefined : 'eval-source-map',

        optimization: {
            minimizer: [
                new TerserPlugin({
                    terserOptions: {
                        compress: false
                    }
                })
            ]
        },

        entry: {
            entry: './src/main.ts'
        },

        output: {
            clean: true,
            path: path.resolve(__dirname, 'dist'),
            filename: production
                ? '[name].[contenthash].bundle.js'
                : '[name].bundle.js',
            // ADR-003 §8: los chunks se piden a este servidor, no al del Shell.
            publicPath: 'auto',
            uniqueName: REMOTE_NAME
        },

        resolve: {
            extensions: ['.ts', '.js'],
            modules: [
                path.resolve(__dirname, 'src'),
                'node_modules'
            ]
            // Sin alias de desarrollo: hay que resolver los mismos paquetes de Aurelia que se comparten con el Shell.
        },

        devServer: {
            historyApiFallback: true,
            open: !process.env.CI,
            port: PORT,
            headers: {
                'Access-Control-Allow-Origin': '*'
            }
        },

        module: {
            rules: [
                {
                    test: /\.(png|svg|jpg|jpeg|gif)$/i,
                    type: 'asset'
                },
                {
                    test: /\.(woff|woff2|ttf|eot|svg|otf)(\?v=[0-9]\.[0-9]\.[0-9])?$/i,
                    type: 'asset'
                },
                {
                    test: /\.css$/i,
                    use: [
                        'style-loader',
                        cssLoader,
                        postcssLoader
                    ]
                },
                {
                    test: /\.ts$/i,
                    use: [
                        'ts-loader',
                        '@aurelia/webpack-loader'
                    ],
                    exclude: /node_modules/
                },
                {
                    test: /[/\\]src[/\\].+\.html$/i,
                    use: '@aurelia/webpack-loader',
                    exclude: /node_modules/
                }
            ]
        },

        plugins: [
            new ModuleFederationPlugin({
                name: REMOTE_NAME,
                filename: 'remoteEntry.js',
                exposes: {
                    './GameModule': './src/game-module'
                },
                shared: sharedDeps
            }),

            new HtmlWebpackPlugin({
                template: 'index.html',
                favicon: 'favicon.ico'
            }),

            new Dotenv({
                path: `./.env${production
                        ? ''
                        : '.' + (process.env.NODE_ENV || 'development')
                    }`
            }),

            analyze && new BundleAnalyzerPlugin()
        ].filter(p => p)
    };
};
