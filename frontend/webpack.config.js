/* eslint-disable @typescript-eslint/no-var-requires */
const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const { ModuleFederationPlugin } = require('webpack').container;
const { BundleAnalyzerPlugin } = require('webpack-bundle-analyzer');
const Dotenv = require('dotenv-webpack');
const TerserPlugin = require('terser-webpack-plugin');

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
            publicPath: 'auto'
        },

        resolve: {
            extensions: ['.ts', '.js'],
            modules: [
                path.resolve(__dirname, 'src'),
                'node_modules'
            ],
            alias: production
                ? {
                    // add your production aliases here
                }
                : {
                    ...getAureliaDevAliases()
                    // add your development aliases here
                }
        },

        devServer: {
            historyApiFallback: true,
            open: !process.env.CI,
            port: 4001,
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
                name: 'typingGame',
                filename: 'remoteEntry.js',
                exposes: {
                    './GameModule': './src/game-module'
                },
                shared: {
                    aurelia: {
                        singleton: true,
                        strictVersion: true,
                        requiredVersion: '2.0.0-rc.2'
                    },
                    '@aurelia/router': {
                        singleton: true,
                        strictVersion: true,
                        requiredVersion: '2.0.0-rc.2'
                    }
                }
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

function getAureliaDevAliases() {
    return [
        'aurelia',
        'fetch-client',
        'kernel',
        'metadata',
        'platform',
        'platform-browser',
        'route-recognizer',
        'router',
        'router-lite',
        'runtime',
        'runtime-html',
        'testing',
        'state',
        'ui-virtualization'
    ].reduce((map, pkg) => {
        const name = pkg === 'aurelia'
            ? pkg
            : `@aurelia/${pkg}`;

        try {
            const packageLocation = require.resolve(name);

            map[name] = path.resolve(
                packageLocation,
                '../../esm/index.dev.mjs'
            );
        } catch {
            // Package alias not available
        }

        return map;
    }, {});
}