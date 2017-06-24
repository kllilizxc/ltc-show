var effectDiv, sourceDiv, canvas, gl, buffer,
vertex_shader, fragment_shader, currentProgram,
vertexPositionLocation, textureLocation,
parameters = { start_time: new Date().getTime(), time: 0, screenWidth: 0, screenHeight: 0 };

var g_zoom = 0;

var model      = new Matrix4x4();
var view       = new Matrix4x4();
var projection = new Matrix4x4();
var controller = null;

var ltc_mat_texture = null;
var ltc_mag_texture = null;

var g_sample_count = 0;

var roughness = 0.25;
var intensity = 4;
var width = 8;
var height = 8;
var rotationY = 0;
var rotationZ = 0;
var twoSided = false;

window.onload = function() {
  init();
}

function FetchFile(url, cache)
{
  var text = $.ajax({
    url:   url,
    async: false,
    dataType: "text",
    mimeType: "text/plain",
    cache: cache,
  }).responseText;

  return text;
}

function SetClampedTextureState()
{
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
}

function init() {
  vertex_shader   = FetchFile("shaders/raw/ltc.vs", false);
  fragment_shader = FetchFile("shaders/raw/ltc.fs", false);

  canvas = document.createElement("canvas");
  canvas.style.cssText = "border: 2px solid #404040; border-radius: 6px";

  effectDiv = document.getElementById("effect");
  effectDiv.appendChild(canvas);

  // Initialise WebGL
  try {
    gl = canvas.getContext("experimental-webgl");
  } catch(error) { }

  if (!gl) {
    alert("WebGL not supported");
    throw "cannot create webgl context";
  }

  // Check for float-RT support
  if (!gl.getExtension("OES_texture_float")) {
    alert("OES_texture_float not supported");
    throw "missing webgl extension";
  }

  if (!gl.getExtension("OES_texture_float_linear")) {
    alert("OES_texture_float_linear not supported");
    throw "missing webgl extension";
  }

  // Create Vertex buffer (2 triangles)
  buffer = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1.0, -1.0, 1.0, -1.0, -1.0, 1.0, 1.0, -1.0, 1.0, 1.0, -1.0, 1.0]), gl.STATIC_DRAW);

  // Create Program

  currentProgram = createProgram(vertex_shader, fragment_shader);

  onWindowResize();
  window.addEventListener("resize", onWindowResize, false);

  $("canvas").mousewheel(function(event, delta) {
    g_zoom += delta*10.0;
    //    console.log("pageX: " + event.pageX + " pageY: " + event.pageY);
    return false;
  });

  initParams();
  bindListeners();


  ltc_mat_texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, ltc_mat_texture);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 64, 64, 0, gl.RGBA, gl.FLOAT, new Float32Array(g_ltc_mat));
  SetClampedTextureState();

  ltc_mag_texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, ltc_mag_texture);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.ALPHA, 64, 64, 0, gl.ALPHA, gl.FLOAT, new Float32Array(g_ltc_mag));
  SetClampedTextureState();


  // Fetch media and kick off the main program once everything has loaded
  $(function() {
    $(document).ajaxStop(function() {
      $(this).unbind("ajaxStop");
      main_prog();
    });

    // Load data here

    main_prog();
  });
}


function onWindowResize(event) {
  canvas.width  = 512; // window.innerWidth;
  canvas.height = 512; // window.innerHeight;

  parameters.screenWidth = canvas.width;
  parameters.screenHeight = canvas.height;

  gl.viewport(0, 0, canvas.width, canvas.height);
}

function createProgram(vertex, fragment) {
  var header = "#ifdef GL_ES\nprecision highp float;\n#endif\n#line 0\n";
  fragment = header + fragment;

  var program = gl.createProgram();

  var vs = createShader(vertex, gl.VERTEX_SHADER);
  var fs = createShader(fragment, gl.FRAGMENT_SHADER);

  if (vs == null || fs == null)
    return null;

  gl.attachShader(program, vs);
  gl.attachShader(program, fs);

  gl.deleteShader(vs);
  gl.deleteShader(fs);

  gl.linkProgram(program);


  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {

    console.error(gl.getProgramInfoLog(program));
    
    return null;
  }

  return program;
}


var gl_shader_error = null;

function createShader(src, type) {
  var shader = gl.createShader(type);

  gl.shaderSource(shader, src);
  gl.compileShader(shader);

  gl_shader_error = null;

  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    gl_shader_error = gl.getShaderInfoLog(shader);
    console.log((type == gl.VERTEX_SHADER ? "VERTEX" : "FRAGMENT") + " SHADER:\n" + gl.getShaderInfoLog(shader));
    return null;
  }

  return shader;
}


function animate() {
  requestAnimationFrame(animate);
  draw();
}

function main_prog() {
  controller = new CameraController(canvas);
  // Try the following (and uncomment the "pointer-events: none;" in
  // the index.html) to try the more precise hit detection
  //  controller = new CameraController(document.getElementById("body"), c, gl);
  controller.onchange = function(xRot, yRot) {
    draw();
    g_sample_count = 0;
  };

  requestAnimationFrame(animate);

  draw();
}


function checkGLError() {
  var error = gl.getError();
  if (error != gl.NO_ERROR) {
    var str = "GL Error: " + error + " " + gl.enum_strings[error];
    console.log(str);
    throw str;
  }
}

function initParams() {
  roughness = $('#roughness-slider')[0].value;
  intensity = $('#intensity-slider')[0].value;
  width = $('#width-slider')[0].value;
  height = $('#height-slider')[0].value;
  rotationY = $('#rotation-y-slider')[0].value;
  rotationZ = $('#rotation-z-slider')[0].value;
  twoSided = $('#two-sided-checkbox')[0].value;
}


function bindListeners() {
  $('#roughness-slider').change(function(evnet) {
    roughness = evnet.target.value;
  });

  $('#intensity-slider').change(function(evnet) {
    intensity = evnet.target.value;
  });

  $('#width-slider').change(function(evnet) {
    width = evnet.target.value;
  });

  $('#height-slider').change(function(evnet) {
    height = evnet.target.value;
  });

  $('#rotation-y-slider').change(function(evnet) {
    rotationY = evnet.target.value;
  });

  $('#rotation-z-slider').change(function(evnet) {
    rotationZ = evnet.target.value;
  });

  $('#two-sided-checkbox').change(function(evnet) {
    twoSided = evnet.target.checked;
  });
}

function draw() {
  parameters.time = new Date().getTime() - parameters.start_time;

  gl.enable(gl.DEPTH_TEST);
  gl.clearColor(0.0, 0.0, 0.0, 0.0);

  // Note: the viewport is automatically set up to cover the entire Canvas.
  gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

  checkGLError();

  // Load program into GPU
  gl.useProgram(currentProgram);

  checkGLError();

  // Add in camera controller's rotation
  view.loadIdentity();
  view.translate(0, 6, 0.1*g_zoom - 0.5);
  view.rotate(controller.xRot - 10.0, 1, 0, 0);
  view.rotate(controller.yRot, 0, 1, 0);;

  // Get var locations
  vertexPositionLocation = gl.getAttribLocation(currentProgram, "position");

  function location(u) {
    return gl.getUniformLocation(currentProgram, u);
  }

  gl.uniform3f(location("lightColor"), 1, 1, 1);
  gl.uniform1f(location("roughness"), roughness);
  gl.uniform3f(location("diffuseColor"), 1, 1, 1);
  gl.uniform3f(location("specularColor"), 1, 1, 1);
  gl.uniform1f(location("intensity"), intensity);
  gl.uniform1f(location("width"), width);
  gl.uniform1f(location("height"), height);
  gl.uniform1f(location("rotationY"), rotationY);
  gl.uniform1f(location("rotationZ"), rotationZ);
  gl.uniform1i(location("twoSided"), twoSided);


  gl.uniformMatrix4fv(location("view"), gl.FALSE, new Float32Array(view.elements));
  gl.uniform2f(location("resolution"), parameters.screenWidth, parameters.screenHeight);

  gl.activeTexture(gl.TEXTURE0);
  gl.bindTexture(gl.TEXTURE_2D, ltc_mat_texture);
  gl.uniform1i(gl.getUniformLocation(currentProgram, "ltc_mat"), 0);

  gl.activeTexture(gl.TEXTURE1);
  gl.bindTexture(gl.TEXTURE_2D, ltc_mag_texture);
  gl.uniform1i(gl.getUniformLocation(currentProgram, "ltc_mag"), 1);

  checkGLError();

  gl.enable(gl.BLEND);
  gl.blendFunc(gl.ONE, gl.ONE);

  // Render geometry
  gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
  gl.vertexAttribPointer(vertexPositionLocation, 2, gl.FLOAT, false, 0, 0);
  gl.enableVertexAttribArray(vertexPositionLocation);
  gl.drawArrays(gl.TRIANGLES, 0, 6);
  gl.disableVertexAttribArray(vertexPositionLocation);

  gl.bindTexture(gl.TEXTURE_2D, null);
}